using System.Security.Cryptography;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InstantPay.Application.Services;

public sealed class AdminOnboardingService : IAdminOnboardingService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly ILogger<AdminOnboardingService> _logger;
    public AdminOnboardingService(AppDbContext db, IEmailService emailService, ILogger<AdminOnboardingService> logger)
    { _db = db; _emailService = emailService; _logger = logger; }

    public async Task<AdminOnboardingPagedResponse> GetListAsync(AdminOnboardingListQuery query, CancellationToken ct)
    {
        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value.Date > query.ToDate.Value.Date)
            throw new ArgumentException("From Date cannot be later than To Date.");
        var users = from user in _db.TblUsers.AsNoTracking()
                    join sales in _db.TblUsers.AsNoTracking() on user.Stid equals sales.Id.ToString() into salesJoin
                    from sales in salesJoin.DefaultIfEmpty()
                    where user.OnboardingStatus != null && user.Stid != null &&
                          (user.Usertype == "RT" || user.Usertype == "AD" || user.Usertype == "MD")
                    select new { user, SalesName = sales.Name ?? sales.Username ?? "" };
        if (query.SalesTeamId.HasValue) users = users.Where(x => x.user.Stid == query.SalesTeamId.Value.ToString());
        if (string.Equals(query.Status, "Review", StringComparison.OrdinalIgnoreCase))
            users = users.Where(x => x.user.OnboardingStatus == OnboardingStatuses.PendingReview || x.user.OnboardingStatus == OnboardingStatuses.PendingReReview);
        else if (!string.IsNullOrWhiteSpace(query.Status)) users = users.Where(x => x.user.OnboardingStatus == query.Status);
        if (query.FromDate.HasValue) users = users.Where(x => x.user.RegDate >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue) users = users.Where(x => x.user.RegDate < query.ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(x => (x.user.Username ?? "").Contains(term) || (x.user.Phone ?? "").Contains(term) ||
                (x.user.EmailId ?? "").Contains(term) || (x.user.PanCard ?? "").Contains(term) || (x.user.AadharCard ?? "").Contains(term));
        }
        var total = await users.CountAsync(ct);
        var raw = await users.OrderByDescending(x => x.user.SubmittedAt ?? x.user.RegDate).ThenByDescending(x => x.user.Id)
            .Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new { x.user.Id, x.user.Name, x.user.Username, x.user.Phone, x.user.EmailId, x.user.Usertype,
                x.user.PanCard, x.user.AadharCard, x.user.OnboardingStatus, x.user.OnboardingVersion, x.user.Stid, x.SalesName,
                DisplayDate = x.user.SubmittedAt ?? x.user.LastDraftSavedAt ?? x.user.RegDate }).ToListAsync(ct);
        var data = raw.Select(x => new AdminOnboardingListItem(x.Id, x.Name ?? "", x.Username ?? "", x.Phone ?? "",
            x.EmailId ?? "", x.Usertype ?? "", x.PanCard ?? "", MaskAadhaar(x.AadharCard), x.OnboardingStatus ?? "", x.OnboardingVersion,
            int.TryParse(x.Stid, out var salesTeamId) ? salesTeamId : 0, x.SalesName, x.DisplayDate)).ToList();
        return new AdminOnboardingPagedResponse(data, total, query.PageIndex, query.PageSize);
    }

    public async Task<AdminOnboardingReviewDetail> GetDetailAsync(int userId, CancellationToken ct)
    {
        var u = await _db.TblUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.OnboardingStatus != null && x.Stid != null &&
            (x.Usertype == "RT" || x.Usertype == "AD" || x.Usertype == "MD"), ct)
            ?? throw new KeyNotFoundException("Onboarding not found.");
        if (u.OnboardingStatus == OnboardingStatuses.PendingReReview)
            await RepairLegacyReReviewFieldStatuses(userId, ct);
        var review = await CurrentReview(userId, ct);
        var fields = review == null ? [] : await _db.TblUserOnboardingFieldReviews.AsNoTracking().Where(x => x.ReviewId == review.Id)
            .OrderBy(x => x.FieldName).Select(x => (object)new { x.Id, x.FieldName, x.ReviewStatus, x.RejectionRemarks, x.ReviewedAt }).ToListAsync(ct);
        var documentRows = await _db.TblUserOnboardingDocuments.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.DocumentType).ToListAsync(ct);
        var versionRows = await _db.TblUserOnboardingDocumentVersions.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.VersionNumber).ToListAsync(ct);
        var documents = documentRows.Select(x => (object)new { x.Id, x.DocumentType, x.CurrentVersion, x.ReviewStatus, x.RejectionRemarks, x.ReviewedAt,
            Versions = versionRows.Where(v => v.DocumentId == x.Id).Select(v => new { v.Id, v.VersionNumber, v.OriginalFileName, v.FileSize, v.CorrectionRemarks, v.UploadedAt }).ToList() }).ToList();
        var historyRows = await _db.TblUserOnboardingHistory.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var actorIds = historyRows.Select(x => x.ActorUserId).Distinct().ToList();
        var actorNames = await _db.TblUsers.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Username ?? $"User #{x.Id}", ct);
        var history = historyRows.Select(x => (object)new { x.Id, x.OnboardingVersion, x.EventType, x.FromStatus, x.ToStatus, x.Remarks, x.ActorUserId,
            x.ActorUserType, ActorName = actorNames.GetValueOrDefault(x.ActorUserId, $"User #{x.ActorUserId}"), x.IpAddress, x.UserAgent, x.CreatedAt }).ToList();
        var credentialDelivery = await _db.TblUserCredentialDeliveryLogs.AsNoTracking().Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt)
            .Select(x => new { x.Id, x.DeliveryStatus, x.DestinationMasked, x.AttemptCount, x.CreatedAt, x.SentAt, x.FailureReason }).FirstOrDefaultAsync(ct);
        var salesTeam = int.TryParse(u.Stid, out var salesTeamId)
            ? await _db.TblUsers.AsNoTracking().Where(x => x.Id == salesTeamId).Select(x => new { x.Id, x.Name, x.Phone }).FirstOrDefaultAsync(ct)
            : null;
        var whiteLabel = int.TryParse(u.Wlid, out var whiteLabelId)
            ? await _db.TblWlUsers.AsNoTracking().Where(x => x.Id == whiteLabelId).Select(x => new { x.Id, Name = x.CompanyName ?? x.UserName, x.Phone }).FirstOrDefaultAsync(ct)
            : null;
        var commissionPlan = u.CommissionPlanId.HasValue
            ? await _db.PlanDetails.AsNoTracking().Where(x => x.Id == u.CommissionPlanId.Value).Select(x => new { x.Id, x.PlanName }).FirstOrDefaultAsync(ct)
            : null;
        var user = new { u.Id, u.Usertype, u.CompanyName, u.Name, u.FatherName, u.Username, u.EmailId, u.Phone, u.PanCard,
            AadhaarMasked = MaskAadhaar(u.AadharCard), u.AddressLine1, u.AddressLine2, u.State, u.City, u.Pincode, u.ShopAddress,
            u.ShopState, u.ShopCity, ShopZipCode = u.ShipZipcode, u.Lat, u.Longitute, u.Wlid, u.Adid, u.Mdid, u.Stid,
            u.CommissionPlanId, CommissionPlanName = commissionPlan?.PlanName, WhiteLabelName = whiteLabel?.Name, WhiteLabelPhone = whiteLabel?.Phone,
            SalesPersonName = salesTeam?.Name, SalesPersonPhone = salesTeam?.Phone, u.IsEmailVerified, u.IsPhoneVerified, u.IsPanVerified, u.IsAadhaarVerified,
            u.OnboardingStatus, u.OnboardingVersion, u.SubmittedAt, u.FinalReviewRemarks };
        return new AdminOnboardingReviewDetail(userId, user, review == null ? new { } : new { review.Id, review.SubmissionVersion, review.ReviewStatus, review.FinalRemarks, CredentialDelivery = credentialDelivery }, fields, documents, history, Convert.ToBase64String(u.RowVersion ?? []));
    }

    public async Task<OnboardingCommandResult> ReviewFieldAsync(int userId, long fieldReviewId, ReviewDecisionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var status = ValidateDecision(request);
        var field = await _db.TblUserOnboardingFieldReviews.SingleOrDefaultAsync(x => x.Id == fieldReviewId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Review field not found.");
        await EnsureReviewable(userId, ct);
        field.ReviewStatus = status; field.RejectionRemarks = status == OnboardingReviewStatuses.Rejected ? request.Remarks!.Trim() : null;
        field.ReviewedBy = adminId; field.ReviewedAt = DateTime.UtcNow;
        await AddHistory(userId, "FieldReviewed", $"{field.FieldName}: {status}. {request.Remarks}".Trim(), adminId, ipAddress, userAgent, ct);
        return new OnboardingCommandResult(true, "Field review saved.", userId, status);
    }

    public async Task<OnboardingCommandResult> ReviewDocumentAsync(int userId, long documentId, ReviewDecisionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var status = ValidateDecision(request);
        var doc = await _db.TblUserOnboardingDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.UserId == userId, ct)
            ?? throw new KeyNotFoundException("Document not found.");
        await EnsureReviewable(userId, ct);
        doc.ReviewStatus = status; doc.RejectionRemarks = status == OnboardingReviewStatuses.Rejected ? request.Remarks!.Trim() : null;
        doc.ReviewedBy = adminId; doc.ReviewedAt = DateTime.UtcNow;
        await AddHistory(userId, "DocumentReviewed", $"{doc.DocumentType}: {status}. {request.Remarks}".Trim(), adminId, ipAddress, userAgent, ct);
        return new OnboardingCommandResult(true, "Document review saved.", userId, status);
    }

    public async Task<OnboardingCommandResult> RejectAsync(int userId, FinalRejectionRequest request, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var user = await EnsureReviewable(userId, ct);
        var review = await CurrentReview(userId, ct) ?? throw new InvalidOperationException("Review session not found.");
        var hasRejectedField = await _db.TblUserOnboardingFieldReviews.AnyAsync(x => x.ReviewId == review.Id && x.ReviewStatus == OnboardingReviewStatuses.Rejected, ct);
        var hasRejectedDoc = await _db.TblUserOnboardingDocuments.AnyAsync(x => x.UserId == userId && x.ReviewStatus == OnboardingReviewStatuses.Rejected, ct);
        if (!hasRejectedField && !hasRejectedDoc) throw new InvalidOperationException("Reject at least one field or document before final rejection.");
        var from = user.OnboardingStatus!; user.OnboardingStatus = OnboardingStatuses.Rejected; user.Status = "Inactive";
        user.FinalReviewRemarks = request.Remarks.Trim(); user.RejectedAt = DateTime.UtcNow; user.RejectedBy = adminId;
        review.ReviewStatus = OnboardingReviewStatuses.Rejected; review.FinalRemarks = request.Remarks.Trim(); review.ReviewedBy = adminId; review.CompletedAt = DateTime.UtcNow;
        _db.TblUserOnboardingHistory.Add(History(user, "Rejected", from, user.OnboardingStatus, request.Remarks, adminId, ipAddress, userAgent));
        await _db.SaveChangesAsync(ct);
        return new OnboardingCommandResult(true, "Onboarding rejected and returned to Sales Team.", userId, user.OnboardingStatus);
    }

    public async Task<OnboardingCommandResult> ApproveAsync(int userId, string? rowVersion, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var user = await EnsureReviewable(userId, ct); ApplyRowVersion(user, rowVersion);
        var review = await CurrentReview(userId, ct) ?? throw new InvalidOperationException("Review session not found.");
        if (await _db.TblUserOnboardingFieldReviews.AnyAsync(x => x.ReviewId == review.Id && x.ReviewStatus != OnboardingReviewStatuses.Approved, ct))
            throw new InvalidOperationException("Every required information section must be approved.");
        var documents = await _db.TblUserOnboardingDocuments.Where(x => x.UserId == userId).ToListAsync(ct);
        if (documents.Any(x => x.ReviewStatus != OnboardingReviewStatuses.Approved))
            throw new InvalidOperationException("Every uploaded document must be approved.");
        if (await _db.TblUsers.AnyAsync(x => x.Id != userId && x.Status == "Active" &&
            (x.Username == user.Username || x.Phone == user.Phone || x.EmailId == user.EmailId || x.PanCard == user.PanCard || x.AadharCard == user.AadharCard), ct))
            throw new InvalidOperationException("An active user already exists with the same username, phone, email, PAN, or Aadhaar.");

        var temporaryPassword = CreateTemporaryPassword();
        var from = user.OnboardingStatus!;
        var merchantCode = await CreateUniqueMerchantCodeAsync(userId, ct);
        var approvedFiles = CopyApprovedDocuments(userId, documents);
        var delivery = new TblUserCredentialDeliveryLog { UserId = userId, Channel = "Email", DestinationMasked = MaskEmail(user.EmailId),
            DeliveryStatus = "Pending", IdempotencyKey = $"onboarding-approved:{userId}:v{user.OnboardingVersion}", AttemptCount = 0, CreatedAt = DateTime.UtcNow };
        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                user.Password = temporaryPassword; user.MPin = "1234"; user.TxnPin = "1234"; user.SuperAdminId = 1;
                user.MobileRecharge = "Active"; user.MoneyTransfer = "Active"; user.Aeps = "Active"; user.AepsStatus = "Active";
                user.BillPayment = "Active"; user.MicroAtm = "Active"; user.RazorpayPayment = "Active"; user.Settlement = "Active";
                user.MerchargeCode = merchantCode; user.Latlongstatus = "Y"; user.Wlid = "1";
                user.Pancopy = approvedFiles.PanCopy; user.AadharFront = approvedFiles.AadhaarFront; user.AadharBack = approvedFiles.AadhaarBack;
                user.Logo = approvedFiles.Logo; user.SelfieImage = approvedFiles.Selfie;
                user.Status = "Active"; user.OnboardingStatus = OnboardingStatuses.Approved;
                user.ApprovedAt = DateTime.UtcNow; user.ApprovedBy = adminId; user.RejectedAt = null; user.RejectedBy = null; user.FinalReviewRemarks = null;
                review.ReviewStatus = OnboardingReviewStatuses.Approved; review.ReviewedBy = adminId; review.CompletedAt = DateTime.UtcNow;
                _db.TblUserCredentialDeliveryLogs.Add(delivery);
                _db.TblUserOnboardingHistory.Add(History(user, "Approved", from, user.OnboardingStatus, "All review items approved and final user defaults applied.", adminId, ipAddress, userAgent));
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch
        {
            DeleteCopiedDocuments(approvedFiles);
            throw;
        }

        var result = await _emailService.SendNewUserWelcomeEmailAsync(user.EmailId!, user.Name ?? user.Username ?? "User", user.Username!, user.Phone ?? "", user.Usertype!, LoginUrl(user.Usertype), temporaryPassword);
        delivery.AttemptCount = 1;
        if (result == "1") { delivery.DeliveryStatus = "Sent"; delivery.SentAt = DateTime.UtcNow; }
        else { delivery.DeliveryStatus = "Failed"; delivery.FailureReason = result[..Math.Min(result.Length, 2000)]; _logger.LogWarning("Credential email failed for user {UserId}", userId); }
        await _db.SaveChangesAsync(ct);
        return new OnboardingCommandResult(true, delivery.DeliveryStatus == "Sent" ? "Onboarding approved and credentials emailed." : "Onboarding approved; credential email is pending retry.", userId, user.OnboardingStatus);
    }

    public async Task<OnboardingCommandResult> RetryCredentialEmailAsync(int userId, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var user = await _db.TblUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Stid != null &&
            (x.Usertype == "RT" || x.Usertype == "AD" || x.Usertype == "MD"), ct) ?? throw new KeyNotFoundException("Onboarding not found.");
        if (user.OnboardingStatus != OnboardingStatuses.Approved || user.Status != "Active") throw new InvalidOperationException("Credentials can only be retried for an approved active user.");
        var latest = await _db.TblUserCredentialDeliveryLogs.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Credential delivery record not found.");
        if (latest.DeliveryStatus == "Sent") throw new InvalidOperationException("Credentials were already delivered successfully.");
        var temporaryPassword = CreateTemporaryPassword();
        user.Password = temporaryPassword;
        latest.DeliveryStatus = "Pending"; latest.AttemptCount++; latest.FailureReason = null;
        await _db.SaveChangesAsync(ct);
        var result = await _emailService.SendNewUserWelcomeEmailAsync(user.EmailId!, user.Name ?? user.Username ?? "User", user.Username!, user.Phone ?? "", user.Usertype!, LoginUrl(user.Usertype), temporaryPassword);
        if (result == "1") { latest.DeliveryStatus = "Sent"; latest.SentAt = DateTime.UtcNow; }
        else { latest.DeliveryStatus = "Failed"; latest.FailureReason = result[..Math.Min(result.Length, 2000)]; }
        _db.TblUserOnboardingHistory.Add(History(user, "CredentialEmailRetried", user.OnboardingStatus, user.OnboardingStatus!, latest.DeliveryStatus, adminId, ipAddress, userAgent));
        await _db.SaveChangesAsync(ct);
        return new OnboardingCommandResult(true, latest.DeliveryStatus == "Sent" ? "Credentials emailed successfully." : "Credential email retry failed and was logged.", userId, user.OnboardingStatus!);
    }

    private async Task<string> CreateUniqueMerchantCodeAsync(int userId, CancellationToken ct)
    {
        var code = $"IP{userId:D8}";
        if (!await _db.TblUsers.AsNoTracking().AnyAsync(x => x.Id != userId && x.MerchargeCode == code, ct)) return code;
        do code = $"IP{RandomNumberGenerator.GetInt32(10_000_000, 100_000_000)}";
        while (await _db.TblUsers.AsNoTracking().AnyAsync(x => x.MerchargeCode == code, ct));
        return code;
    }

    private static ApprovedDocumentCopies CopyApprovedDocuments(int userId, IReadOnlyCollection<TblUserOnboardingDocument> documents)
    {
        var copiedFiles = new List<string>();
        try
        {
            string Copy(string documentType, string destinationFolder)
            {
                var document = documents.SingleOrDefault(x => x.DocumentType.Equals(documentType, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"Approved {documentType} document is missing.");
                var webRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
                var source = Path.GetFullPath(Path.Combine(webRoot, document.CurrentFilePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!source.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
                    throw new InvalidOperationException($"Approved {documentType} file is unavailable.");

                var extension = Path.GetExtension(source).ToLowerInvariant();
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var relativePath = Path.Combine("UploadFiles", "ClientUser", userId.ToString(), destinationFolder, fileName).Replace('\\', '/');
                var destination = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination, false);
                copiedFiles.Add(destination);
                return relativePath;
            }

            return new ApprovedDocumentCopies(
                Copy("PanCopy", "PanCard"),
                Copy("AadhaarFront", "AadharCard"),
                Copy("AadhaarBack", "AadharBack"),
                Copy("Logo", "Logo"),
                Copy("Selfie", "Selfie"),
                copiedFiles);
        }
        catch
        {
            foreach (var file in copiedFiles) if (File.Exists(file)) File.Delete(file);
            throw;
        }
    }

    private static void DeleteCopiedDocuments(ApprovedDocumentCopies files)
    {
        foreach (var file in files.AbsolutePaths) if (File.Exists(file)) File.Delete(file);
    }

    private sealed record ApprovedDocumentCopies(
        string PanCopy, string AadhaarFront, string AadhaarBack, string Logo, string Selfie,
        IReadOnlyCollection<string> AbsolutePaths);

    private async Task<TblUser> EnsureReviewable(int userId, CancellationToken ct)
    {
        var user = await _db.TblUsers.SingleOrDefaultAsync(x => x.Id == userId && x.Stid != null &&
            (x.Usertype == "RT" || x.Usertype == "AD" || x.Usertype == "MD"), ct) ?? throw new KeyNotFoundException("Onboarding not found.");
        if (user.OnboardingStatus is not (OnboardingStatuses.PendingReview or OnboardingStatuses.PendingReReview)) throw new InvalidOperationException("Onboarding is not pending review.");
        return user;
    }
    private Task<TblUserOnboardingReview?> CurrentReview(int userId, CancellationToken ct) => _db.TblUserOnboardingReviews.Where(x => x.UserId == userId).OrderByDescending(x => x.SubmissionVersion).FirstOrDefaultAsync(ct);
    private async Task RepairLegacyReReviewFieldStatuses(int userId, CancellationToken ct)
    {
        var reviewIds = await _db.TblUserOnboardingReviews.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SubmissionVersion)
            .Select(x => x.Id)
            .Take(2)
            .ToListAsync(ct);
        if (reviewIds.Count < 2) return;

        var currentFields = await _db.TblUserOnboardingFieldReviews
            .Where(x => x.ReviewId == reviewIds[0] && x.ReviewStatus == OnboardingReviewStatuses.Pending)
            .ToListAsync(ct);
        if (currentFields.Count == 0) return;

        var decisionHistory = await (from review in _db.TblUserOnboardingReviews.AsNoTracking()
            join field in _db.TblUserOnboardingFieldReviews.AsNoTracking() on review.Id equals field.ReviewId
            where review.UserId == userId && review.Id != reviewIds[0] && field.ReviewStatus != OnboardingReviewStatuses.Pending
            orderby review.SubmissionVersion descending
            select new { field.FieldName, field.ReviewStatus, field.ReviewedBy, field.ReviewedAt }).ToListAsync(ct);
        var latestDecisions = decisionHistory
            .GroupBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var repaired = false;
        foreach (var field in currentFields)
        {
            if (!latestDecisions.TryGetValue(field.FieldName, out var previous) ||
                previous.ReviewStatus != OnboardingReviewStatuses.Approved) continue;
            field.ReviewStatus = OnboardingReviewStatuses.Approved;
            field.ReviewedBy = previous.ReviewedBy;
            field.ReviewedAt = previous.ReviewedAt;
            field.RejectionRemarks = null;
            repaired = true;
        }
        if (repaired) await _db.SaveChangesAsync(ct);
    }
    private async Task AddHistory(int userId, string eventType, string remarks, int adminId, string? ipAddress, string? userAgent, CancellationToken ct)
    { var u = await _db.TblUsers.SingleAsync(x => x.Id == userId, ct); _db.TblUserOnboardingHistory.Add(History(u, eventType, u.OnboardingStatus, u.OnboardingStatus!, remarks, adminId, ipAddress, userAgent)); await _db.SaveChangesAsync(ct); }
    private static TblUserOnboardingHistory History(TblUser u, string type, string? from, string to, string? remarks, int actor, string? ip, string? agent) => new()
    { UserId = u.Id, OnboardingVersion = u.OnboardingVersion, EventType = type, FromStatus = from, ToStatus = to, Remarks = remarks,
      ActorUserId = actor, ActorUserType = "SuperAdmin", IpAddress = Truncate(ip, 64), UserAgent = Truncate(agent, 500), CreatedAt = DateTime.UtcNow };
    private static string ValidateDecision(ReviewDecisionRequest request)
    { var s = request.Status.Trim(); if (s is not (OnboardingReviewStatuses.Approved or OnboardingReviewStatuses.Rejected)) throw new ArgumentException("Status must be Approved or Rejected."); if (s == OnboardingReviewStatuses.Rejected && string.IsNullOrWhiteSpace(request.Remarks)) throw new ArgumentException("Rejection remarks are required."); return s; }
    private void ApplyRowVersion(TblUser user, string? token) { if (string.IsNullOrWhiteSpace(token)) return; try { _db.Entry(user).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(token); } catch (FormatException) { throw new ArgumentException("Invalid record version."); } }
    private static string CreateTemporaryPassword() => $"Ip@{RandomNumberGenerator.GetInt32(100000, 1000000)}{(char)RandomNumberGenerator.GetInt32('A', 'Z' + 1)}";
    private static string LoginUrl(string? type) => type switch { "AD" => "https://instantpayment.in/distributor-login", "MD" => "https://instantpayment.in/masterdistributor-login", _ => "https://instantpayment.in/login" };
    private static string MaskAadhaar(string? v) => string.IsNullOrWhiteSpace(v) || v.Length < 4 ? "" : $"XXXXXXXX{v[^4..]}";
    private static string MaskEmail(string? v) { if (string.IsNullOrWhiteSpace(v) || !v.Contains('@')) return "***"; var p = v.Split('@', 2); return $"{p[0][0]}***@{p[1]}"; }
    private static string? Truncate(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim()[..Math.Min(v.Trim().Length, max)];
}
