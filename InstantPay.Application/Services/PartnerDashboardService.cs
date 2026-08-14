using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.Application.Services;

public sealed class PartnerDashboardService : IPartnerDashboardService
{
    private static readonly string[] SupportedStatuses =
        ["SUCCESS", "PENDING", "FAILED", "PROCESS"];
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromMinutes(330);

    private readonly AppDbContext _dbContext;
    private readonly IWalletService _walletService;

    public PartnerDashboardService(
        AppDbContext dbContext,
        IWalletService walletService)
    {
        _dbContext = dbContext;
        _walletService = walletService;
    }

    public async Task<PartnerDashboardResponse> GetDashboardAsync(
        int partnerId,
        string userType,
        int days,
        CancellationToken cancellationToken)
    {
        var normalizedUserType = NormalizeUserType(userType);
        days = Math.Clamp(days, 1, 30);

        var partnerExists = await _dbContext.TblUsers.AsNoTracking().AnyAsync(
            user => user.Id == partnerId &&
                    user.Usertype == normalizedUserType &&
                    user.Status == "Active",
            cancellationToken);
        if (!partnerExists)
        {
            throw new UnauthorizedAccessException("Partner account is not active.");
        }

        var partnerIdText = partnerId.ToString();
        var scopedUsers = _dbContext.TblUsers
            .AsNoTracking()
            .Where(user => normalizedUserType == "AD"
                ? user.Adid == partnerIdText
                : user.Mdid == partnerIdText);
        var scopedUserIds = scopedUsers.Select(user => user.Id);
        var scopedTransactionUserIds = scopedUsers.Select(user => user.Id.ToString());

        var totalUsers = await scopedUsers.CountAsync(cancellationToken);
        var recentUsers = await scopedUsers
            .OrderByDescending(user => user.RegDate)
            .ThenByDescending(user => user.Id)
            .Take(5)
            .Select(user => new PartnerRecentUserDto(
                user.Id,
                user.Name ?? user.CompanyName ?? "User",
                user.Username ?? string.Empty,
                user.Usertype ?? string.Empty,
                user.Status ?? string.Empty,
                user.RegDate))
            .ToListAsync(cancellationToken);

        var (todayStartUtc, tomorrowStartUtc) = GetIndiaTodayUtcBounds();
        var chartStartUtc = todayStartUtc.AddDays(-(days - 1));
        var transactions = _dbContext.TransactionDetails
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId != null &&
                scopedTransactionUserIds.Contains(transaction.UserId) &&
                transaction.ReqDate.HasValue);

        var todayRows = await transactions
            .Where(transaction =>
                transaction.ReqDate >= todayStartUtc &&
                transaction.ReqDate < tomorrowStartUtc &&
                transaction.Status != null &&
                SupportedStatuses.Contains(transaction.Status.Trim().ToUpper()))
            .GroupBy(transaction => transaction.Status!.Trim().ToUpper())
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Amount = group.Sum(transaction => transaction.Amount ?? 0m)
            })
            .ToListAsync(cancellationToken);

        var todayStatusSummary = SupportedStatuses
            .Select(status =>
            {
                var row = todayRows.FirstOrDefault(item => item.Status == status);
                return new PartnerStatusSummaryDto(
                    ToDisplayStatus(status),
                    row?.Count ?? 0,
                    row?.Amount ?? 0m);
            })
            .ToList();

        var graphRows = await transactions
            .Where(transaction =>
                transaction.ReqDate >= chartStartUtc &&
                transaction.ReqDate < tomorrowStartUtc &&
                transaction.Status != null &&
                SupportedStatuses.Contains(transaction.Status.Trim().ToUpper()))
            .GroupBy(transaction => new
            {
                Date = transaction.ReqDate!.Value.AddMinutes(330).Date,
                Status = transaction.Status!.Trim().ToUpper()
            })
            .Select(group => new
            {
                group.Key.Date,
                group.Key.Status,
                Amount = group.Sum(transaction => transaction.Amount ?? 0m)
            })
            .ToListAsync(cancellationToken);

        var chart = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var indiaDate = chartStartUtc.Add(IndiaOffset).Date.AddDays(offset);
                decimal AmountFor(string status) => graphRows
                    .Where(row => row.Date == indiaDate && row.Status == status)
                    .Sum(row => row.Amount);

                return new PartnerChartPointDto(
                    indiaDate,
                    AmountFor("SUCCESS"),
                    AmountFor("PENDING"),
                    AmountFor("FAILED"),
                    AmountFor("PROCESS"));
            })
            .ToList();

        var recentPaymentRequests = await (
                from payment in _dbContext.TblPaymentRequest.AsNoTracking()
                join user in _dbContext.TblUsers.AsNoTracking()
                    on payment.UserId equals (int?)user.Id into users
                from user in users.DefaultIfEmpty()
                where payment.IsDeleted != true &&
                      payment.UserId.HasValue &&
                      (payment.UserId == partnerId ||
                       scopedUserIds.Contains(payment.UserId.Value))
                orderby payment.CreatedOn descending, payment.PaymentId descending
                select new PartnerPaymentRequestDto(
                    payment.PaymentId,
                    payment.UserId.Value,
                    user != null
                        ? user.Name ?? user.CompanyName ?? user.Username ?? "User"
                        : "User",
                    payment.Amount ?? 0m,
                    payment.Status ?? "Unknown",
                    payment.DeposideMode ?? string.Empty,
                    payment.PaymentTxnId ?? payment.TxnId ?? string.Empty,
                    payment.CreatedOn))
            .Take(8)
            .ToListAsync(cancellationToken);

        var walletAmount = await _walletService.GetBalanceAsync(partnerId, cancellationToken);
        return new PartnerDashboardResponse(
            normalizedUserType,
            totalUsers,
            walletAmount,
            todayStatusSummary.Sum(item => item.Amount),
            todayStatusSummary,
            chart,
            recentUsers,
            recentPaymentRequests,
            DateTime.UtcNow);
    }

    public async Task<PartnerWalletResponse> GetWalletAsync(
        int partnerId,
        CancellationToken cancellationToken) =>
        new(
            await _walletService.GetBalanceAsync(partnerId, cancellationToken),
            DateTime.UtcNow);

    private static string NormalizeUserType(string userType) =>
        userType.ToUpperInvariant() switch
        {
            "AD" => "AD",
            "MD" => "MD",
            _ => throw new UnauthorizedAccessException("Unsupported partner role.")
        };

    private static string ToDisplayStatus(string status) =>
        status[0] + status[1..].ToLowerInvariant();

    private static (DateTime TodayStartUtc, DateTime TomorrowStartUtc)
        GetIndiaTodayUtcBounds()
    {
        var indiaNow = DateTimeOffset.UtcNow.ToOffset(IndiaOffset);
        var todayStart = new DateTimeOffset(indiaNow.Date, IndiaOffset);
        return (todayStart.UtcDateTime, todayStart.AddDays(1).UtcDateTime);
    }
}
