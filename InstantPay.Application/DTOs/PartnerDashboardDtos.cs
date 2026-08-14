namespace InstantPay.Application.DTOs;

public sealed record PartnerDashboardResponse(
    string UserType,
    int TotalUsers,
    decimal WalletAmount,
    decimal TodayTransactionAmount,
    IReadOnlyList<PartnerStatusSummaryDto> TodayStatusSummary,
    IReadOnlyList<PartnerChartPointDto> TransactionChart,
    IReadOnlyList<PartnerRecentUserDto> RecentUsers,
    IReadOnlyList<PartnerPaymentRequestDto> RecentPaymentRequests,
    DateTime GeneratedAtUtc);

public sealed record PartnerStatusSummaryDto(
    string Status,
    int Count,
    decimal Amount);

public sealed record PartnerChartPointDto(
    DateTime Date,
    decimal Success,
    decimal Pending,
    decimal Failed,
    decimal Process);

public sealed record PartnerRecentUserDto(
    int UserId,
    string Name,
    string Username,
    string UserType,
    string Status,
    DateTime? OnboardedAt);

public sealed record PartnerPaymentRequestDto(
    Guid? PaymentId,
    int UserId,
    string UserName,
    decimal Amount,
    string Status,
    string DepositMode,
    string TransactionId,
    DateTime? CreatedOn);

public sealed record PartnerWalletResponse(
    decimal WalletAmount,
    DateTime RefreshedAtUtc);
