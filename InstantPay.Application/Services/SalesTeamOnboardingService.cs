using System.Net.Mail;
using System.Text.RegularExpressions;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using InstantPay.SharedKernel.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace InstantPay.Application.Services;

public sealed class SalesTeamOnboardingService : ISalesTeamOnboardingService
{
    private static readonly Regex PhoneRegex = new("^[6-9][0-9]{9}$", RegexOptions.Compiled);
    private static readonly Regex PanRegex = new("^[A-Z]{5}[0-9]{4}[A-Z]$", RegexOptions.Compiled);
    private static readonly Regex AadhaarRegex = new("^[0-9]{12}$", RegexOptions.Compiled);
    private static readonly Regex CoordinateRegex = new("^-?[0-9]{1,3}(\\.[0-9]{1,4})?$", RegexOptions.Compiled);
    private static readonly string[] RequiredDocuments = ["PanCopy", "AadhaarFront", "AadhaarBack", "Selfie", "Logo"];
    private static readonly string[] ReviewableFields = ["Identity", "Contact", "Business", "Address", "Hierarchy", "Kyc"];
    private static readonly HashSet<string> AllowedDocumentTypes = new(RequiredDocuments.Append("Logo"), StringComparer.OrdinalIgnoreCase);
    private const long MaximumFileSize = 5 * 1024 * 1024;
    private readonly AppDbContext _db;

    public SalesTeamOnboardingService(AppDbContext db) => _db = db;

    public async Task<OnboardingDraftResponse> SaveDraftAsync(SaveOnboardingDraftRequest request, int salesTeamId, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        ValidatePartial(request);
        var mappedWlId = await _db.TblUsers.AsNoTracking()
            .Where(x => x.Id == salesTeamId && x.Usertype == "ST" && x.Status == "Active")
            .Select(x => x.Wlid)
            .SingleOrDefaultAsync(cancellationToken);
        if (!int.TryParse(mappedWlId, out var mappedWlUserId) || mappedWlUserId <= 0)
            throw new InvalidOperationException("Your Sales Team account is not mapped to an active White Label user. Contact SuperAdmin.");
        if (!await _db.TblWlUsers.AsNoTracking().AnyAsync(x => x.Id == mappedWlUserId && x.Status == "Active", cancellationToken))
            throw new InvalidOperationException("The White Label user mapped to your Sales Team account is not active. Contact SuperAdmin.");
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTime.UtcNow;
            TblUser user;
            if (request.UserId == 0)
            {
                user = new TblUser
                {
                    Status = "Inactive", OnboardingStatus = OnboardingStatuses.Draft, OnboardingVersion = 0,
                    Stid = salesTeamId.ToString(), CreatedByUserId = salesTeamId, CreatedByUserType = "ST", RegDate = now,
                    RazorpayPayment = "Inactive", Settlement = "Inactive"
                };
                _db.TblUsers.Add(user);
            }
            else
            {
                user = await GetOwnedEditableAsync(request.UserId, salesTeamId, cancellationToken);
                ApplyConcurrencyToken(user, request.RowVersion);
            }

            Apply(request, user);
            user.Wlid = mappedWlUserId.ToString();
            user.Adid = null;
            user.Mdid = null;
            user.Status = "Inactive";
            user.LastDraftSavedAt = now;
            user.OnboardingStatus = user.OnboardingStatus == OnboardingStatuses.Rejected
                ? OnboardingStatuses.Rejected : OnboardingStatuses.Draft;

            await _db.SaveChangesAsync(cancellationToken);
            _db.TblUserOnboardingHistory.Add(new TblUserOnboardingHistory
            {
                UserId = user.Id, OnboardingVersion = user.OnboardingVersion, EventType = "DraftSaved",
                FromStatus = user.OnboardingStatus, ToStatus = user.OnboardingStatus, ActorUserId = salesTeamId,
                ActorUserType = "ST", IpAddress = Truncate(ipAddress, 64), UserAgent = Truncate(userAgent, 500), CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OnboardingDraftResponse(user.Id, user.OnboardingStatus!, user.OnboardingVersion, Convert.ToBase64String(user.RowVersion ?? []));
        });
    }

    public async Task<OwnedOnboardingDetail?> FindOwnedDraftByPhoneAsync(string phone, int salesTeamId, CancellationToken cancellationToken)
    {
        var normalized = NormalizePhone(phone);
        if (!PhoneRegex.IsMatch(normalized)) throw new ArgumentException("Enter a valid 10-digit mobile number.");
        var id = await _db.TblUsers.AsNoTracking().Where(u => u.Stid == salesTeamId.ToString() && u.Phone == normalized &&
            (u.OnboardingStatus == OnboardingStatuses.Draft || u.OnboardingStatus == OnboardingStatuses.Rejected))
            .Select(u => (int?)u.Id).FirstOrDefaultAsync(cancellationToken);
        return id.HasValue ? await BuildOwnedDetailAsync(id.Value, salesTeamId, cancellationToken) : null;
    }

    public Task<OwnedOnboardingDetail> GetOwnedDetailAsync(int userId, int salesTeamId, CancellationToken cancellationToken) =>
        BuildOwnedDetailAsync(userId, salesTeamId, cancellationToken);

    public async Task<OnboardingPagedResponse> GetOwnedListAsync(OnboardingListQuery query, int salesTeamId, CancellationToken cancellationToken)
    {
        ValidateDateRange(query.FromDate, query.ToDate);
        var users = _db.TblUsers.AsNoTracking().Where(u => u.Stid == salesTeamId.ToString() && u.OnboardingStatus != null);
        if (string.Equals(query.Status, "Review", StringComparison.OrdinalIgnoreCase))
            users = users.Where(u => u.OnboardingStatus == OnboardingStatuses.PendingReview || u.OnboardingStatus == OnboardingStatuses.PendingReReview);
        else if (!string.IsNullOrWhiteSpace(query.Status)) users = users.Where(u => u.OnboardingStatus == query.Status);
        if (query.FromDate.HasValue) users = users.Where(u => u.RegDate >= query.FromDate.Value.Date);
        if (query.ToDate.HasValue) users = users.Where(u => u.RegDate < query.ToDate.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u => (u.Username ?? "").Contains(term) || (u.Phone ?? "").Contains(term) ||
                (u.EmailId ?? "").Contains(term) || (u.PanCard ?? "").Contains(term) || (u.AadharCard ?? "").Contains(term));
        }
        var total = await users.CountAsync(cancellationToken);
        var data = await users.OrderByDescending(u => u.RegDate).ThenByDescending(u => u.Id)
            .Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize)
            .Select(u => new OnboardingListItem(u.Id, u.Name ?? "", u.Username ?? "", u.Phone ?? "", u.EmailId ?? "",
                u.Usertype ?? "", u.PanCard ?? "", MaskAadhaar(u.AadharCard), u.OnboardingStatus ?? "", u.RegDate,
                u.SubmittedAt ?? u.LastDraftSavedAt ?? u.RegDate))
            .ToListAsync(cancellationToken);
        return new OnboardingPagedResponse(data, total, query.PageIndex, query.PageSize);
    }

    public async Task<OnboardingCommandResult> SubmitAsync(int userId, string? rowVersion, int salesTeamId, string? ipAddress, string? userAgent, CancellationToken cancellationToken)
    {
        var user = await GetOwnedEditableAsync(userId, salesTeamId, cancellationToken);
        ApplyConcurrencyToken(user, rowVersion);
        ValidateForSubmission(user);
        if (!await _db.PlanDetails.AsNoTracking().AnyAsync(p => p.Id == user.CommissionPlanId && p.IsActive, cancellationToken))
            throw new InvalidOperationException("Select an active commission plan.");
        if (await _db.TblUsers.AsNoTracking().AnyAsync(x => x.Id != user.Id &&
            (x.Status == "Active" || x.OnboardingStatus == OnboardingStatuses.PendingReview || x.OnboardingStatus == OnboardingStatuses.PendingReReview) &&
            (x.Username == user.Username || x.EmailId == user.EmailId || x.Phone == user.Phone || x.PanCard == user.PanCard || x.AadharCard == user.AadharCard), cancellationToken))
            throw new InvalidOperationException("An active or submitted user already exists with the same username, phone, email, PAN, or Aadhaar.");
        var documentTypes = await _db.TblUserOnboardingDocuments.Where(d => d.UserId == userId).Select(d => d.DocumentType).ToListAsync(cancellationToken);
        var missing = RequiredDocuments.Except(documentTypes, StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException($"Upload required documents: {string.Join(", ", missing)}.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            var from = user.OnboardingStatus!;
            user.OnboardingVersion++;
            user.OnboardingStatus = from == OnboardingStatuses.Rejected ? OnboardingStatuses.PendingReReview : OnboardingStatuses.PendingReview;
            user.SubmittedAt = DateTime.UtcNow; user.FinalReviewRemarks = null;
            var review = new TblUserOnboardingReview
            {
                UserId = user.Id, SubmissionVersion = user.OnboardingVersion, ReviewStatus = OnboardingReviewStatuses.Pending, StartedAt = DateTime.UtcNow
            };
            _db.TblUserOnboardingReviews.Add(review);
            await _db.SaveChangesAsync(cancellationToken);
            _db.TblUserOnboardingFieldReviews.AddRange(ReviewableFields.Select(field => new TblUserOnboardingFieldReview
            {
                ReviewId = review.Id, UserId = user.Id, FieldName = field, ReviewStatus = OnboardingReviewStatuses.Pending
            }));
            _db.TblUserOnboardingHistory.Add(new TblUserOnboardingHistory
            {
                UserId = user.Id, OnboardingVersion = user.OnboardingVersion, EventType = "Submitted",
                FromStatus = from, ToStatus = user.OnboardingStatus, ActorUserId = salesTeamId, ActorUserType = "ST",
                IpAddress = Truncate(ipAddress, 64), UserAgent = Truncate(userAgent, 500), CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OnboardingCommandResult(true, "Onboarding submitted for review.", user.Id, user.OnboardingStatus);
        });
    }

    public async Task<OnboardingDocumentResponse> UploadDocumentAsync(int userId, string documentType, IFormFile file, string? correctionRemarks, int salesTeamId, CancellationToken cancellationToken)
    {
        var user = await GetOwnedEditableAsync(userId, salesTeamId, cancellationToken);
        var normalizedType = AllowedDocumentTypes.FirstOrDefault(x => x.Equals(documentType, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException("Unsupported document type.");
        if (file.Length is <= 0 or > MaximumFileSize) throw new ArgumentException("Document must be between 1 byte and 5 MB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".pdf")) throw new ArgumentException("Only JPG, PNG, and PDF files are allowed.");

        await using var source = file.OpenReadStream();
        var header = new byte[8];
        var headerLength = await source.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        source.Position = 0;
        if (!MatchesSignature(extension, header.AsSpan(0, headerLength))) throw new ArgumentException("File content does not match its extension.");
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(source, cancellationToken));
        source.Position = 0;

        var existing = await _db.TblUserOnboardingDocuments.SingleOrDefaultAsync(
            d => d.UserId == userId && d.DocumentType == normalizedType, cancellationToken);
        if (existing?.ReviewStatus == OnboardingReviewStatuses.Rejected && string.IsNullOrWhiteSpace(correctionRemarks))
            throw new ArgumentException("Correction remarks are required when replacing a rejected document.");

        var version = (existing?.CurrentVersion ?? 0) + 1;
        var relativePath = Path.Combine("UploadFiles", "Onboarding", userId.ToString(), normalizedType,
            $"v{version}_{Guid.NewGuid():N}{extension}").Replace('\\', '/');
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using (var target = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            await source.CopyToAsync(target, cancellationToken);

        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                existing ??= new TblUserOnboardingDocument { UserId = userId, DocumentType = normalizedType, CreatedAt = DateTime.UtcNow };
                if (existing.Id == 0) _db.TblUserOnboardingDocuments.Add(existing);
                existing.CurrentFilePath = relativePath; existing.CurrentVersion = version; existing.ReviewStatus = OnboardingReviewStatuses.Pending;
                existing.RejectionRemarks = null; existing.ReviewedBy = null; existing.ReviewedAt = null; existing.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                _db.TblUserOnboardingDocumentVersions.Add(new TblUserOnboardingDocumentVersion
                {
                    DocumentId = existing.Id, UserId = userId, VersionNumber = version, FilePath = relativePath,
                    OriginalFileName = Path.GetFileName(file.FileName), ContentType = file.ContentType, FileSize = file.Length,
                    FileHash = hash, CorrectionRemarks = Clean(correctionRemarks), UploadedBy = salesTeamId, UploadedAt = DateTime.UtcNow
                });
                _db.TblUserOnboardingHistory.Add(new TblUserOnboardingHistory
                {
                    UserId = userId, OnboardingVersion = user.OnboardingVersion, EventType = version == 1 ? "DocumentUploaded" : "DocumentReuploaded",
                    FromStatus = user.OnboardingStatus, ToStatus = user.OnboardingStatus!, Remarks = $"{normalizedType} version {version}",
                    ActorUserId = salesTeamId, ActorUserType = "ST", CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new OnboardingDocumentResponse(existing.Id, normalizedType, version, existing.ReviewStatus);
            });
        }
        catch
        {
            if (File.Exists(absolutePath)) File.Delete(absolutePath);
            throw;
        }
    }

    private async Task<TblUser> GetOwnedEditableAsync(int userId, int salesTeamId, CancellationToken ct)
    {
        var user = await _db.TblUsers.SingleOrDefaultAsync(u => u.Id == userId && u.Stid == salesTeamId.ToString(), ct)
            ?? throw new KeyNotFoundException("Onboarding not found.");
        if (user.OnboardingStatus is not (OnboardingStatuses.Draft or OnboardingStatuses.Rejected))
            throw new InvalidOperationException("Only Draft or Rejected onboarding can be edited.");
        return user;
    }

    private async Task<OwnedOnboardingDetail> BuildOwnedDetailAsync(int userId, int salesTeamId, CancellationToken ct)
    {
        var u = await _db.TblUsers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId && x.Stid == salesTeamId.ToString() && x.OnboardingStatus != null, ct)
            ?? throw new KeyNotFoundException("Onboarding not found.");
        var docRows = await (from d in _db.TblUserOnboardingDocuments.AsNoTracking()
                          let correction = _db.TblUserOnboardingDocumentVersions.Where(v => v.DocumentId == d.Id)
                              .OrderByDescending(v => v.VersionNumber).Select(v => v.CorrectionRemarks).FirstOrDefault()
                          where d.UserId == userId
                          orderby d.DocumentType
                          select new { d.Id, d.DocumentType, d.CurrentVersion, d.ReviewStatus, d.RejectionRemarks, Correction = correction }).ToListAsync(ct);
        var versionRows = await _db.TblUserOnboardingDocumentVersions.AsNoTracking().Where(v => v.UserId == userId)
            .OrderByDescending(v => v.VersionNumber).Select(v => new OnboardingDocumentVersionItem(v.Id, v.VersionNumber, v.OriginalFileName, v.FileSize, v.CorrectionRemarks, v.UploadedAt)).ToListAsync(ct);
        var versionDocumentIds = await _db.TblUserOnboardingDocumentVersions.AsNoTracking().Where(v => v.UserId == userId)
            .OrderByDescending(v => v.VersionNumber).Select(v => new { v.Id, v.DocumentId }).ToListAsync(ct);
        var docs = docRows.Select(d => new OwnedOnboardingDocument(d.Id, d.DocumentType, d.CurrentVersion, d.ReviewStatus, d.RejectionRemarks, d.Correction,
            versionRows.Where(v => versionDocumentIds.Any(link => link.Id == v.Id && link.DocumentId == d.Id)).ToList())).ToList();
        var historyRows = await _db.TblUserOnboardingHistory.AsNoTracking().Where(h => h.UserId == userId).OrderByDescending(h => h.CreatedAt).ToListAsync(ct);
        var actorIds = historyRows.Select(h => h.ActorUserId).Distinct().ToList();
        var actorNames = await _db.TblUsers.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name ?? x.Username ?? $"User #{x.Id}", ct);
        var history = historyRows.Select(h => new OnboardingHistoryItem(h.Id, h.OnboardingVersion, h.EventType, h.FromStatus, h.ToStatus, h.Remarks,
            h.ActorUserId, h.ActorUserType, actorNames.GetValueOrDefault(h.ActorUserId, $"User #{h.ActorUserId}"), h.IpAddress, h.CreatedAt)).ToList();
        return new OwnedOnboardingDetail(u.Id, u.Usertype, u.CompanyName, u.Name, u.FatherName, u.Username, u.EmailId, u.Phone,
            u.PanCard, u.AadharCard, MaskAadhaar(u.AadharCard), u.AddressLine1, u.AddressLine2, u.State, u.City, u.Pincode, u.ShopAddress,
            u.ShopState, u.ShopCity, u.ShipZipcode, u.Lat, u.Longitute, u.Wlid, u.Adid, u.Mdid, u.CommissionPlanId,
            u.IsEmailVerified, u.IsPhoneVerified, u.IsPanVerified, u.IsAadhaarVerified,
            u.OnboardingStatus!, u.OnboardingVersion, Convert.ToBase64String(u.RowVersion ?? []), u.FinalReviewRemarks, docs, history);
    }

    private static void Apply(SaveOnboardingDraftRequest r, TblUser u)
    {
        var email = Lower(r.EmailId); var phone = NormalizePhone(r.Phone); var pan = Upper(r.PanCard); var aadhaar = Digits(r.AadharCard);
        if (!string.Equals(u.EmailId, email, StringComparison.OrdinalIgnoreCase)) u.IsEmailVerified = false;
        if (!string.Equals(u.Phone, phone, StringComparison.Ordinal)) u.IsPhoneVerified = false;
        if (!string.Equals(u.PanCard, pan, StringComparison.OrdinalIgnoreCase)) u.IsPanVerified = false;
        if (!string.IsNullOrWhiteSpace(r.AadharCard) && !string.Equals(u.AadharCard, aadhaar, StringComparison.Ordinal)) u.IsAadhaarVerified = false;
        u.Usertype = Upper(r.UserType); u.CompanyName = Clean(r.CompanyName); u.Name = Clean(r.Name); u.FatherName = Clean(r.FatherName);
        u.Username = Clean(r.Username); u.EmailId = email; u.Phone = phone; u.PanCard = pan;
        if (!string.IsNullOrWhiteSpace(r.AadharCard)) u.AadharCard = aadhaar;
        u.AddressLine1 = Clean(r.AddressLine1); u.AddressLine2 = Clean(r.AddressLine2);
        u.State = Clean(r.State); u.City = Clean(r.City); u.Pincode = Digits(r.Pincode); u.ShopAddress = Clean(r.ShopAddress);
        u.ShopState = Clean(r.ShopState); u.ShopCity = Clean(r.ShopCity); u.ShipZipcode = Digits(r.ShopZipCode); u.Lat = Clean(r.Latitude);
        u.Longitute = Clean(r.Longitude);
        u.CommissionPlanId = r.CommissionPlanId; u.PlanId = r.CommissionPlanId?.ToString();
    }

    private static void ValidatePartial(SaveOnboardingDraftRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.UserType) && Upper(r.UserType) is not ("RT" or "AD" or "MD")) throw new ArgumentException("User type must be RT, AD, or MD.");
        if (!string.IsNullOrWhiteSpace(r.Phone) && !PhoneRegex.IsMatch(NormalizePhone(r.Phone))) throw new ArgumentException("Enter a valid 10-digit mobile number.");
        if (!string.IsNullOrWhiteSpace(r.PanCard) && !PanRegex.IsMatch(Upper(r.PanCard)!)) throw new ArgumentException("Enter a valid PAN number.");
        if (!string.IsNullOrWhiteSpace(r.AadharCard) && !AadhaarRegex.IsMatch(Digits(r.AadharCard)!)) throw new ArgumentException("Enter a valid 12-digit Aadhaar number.");
        if (!string.IsNullOrWhiteSpace(r.Latitude) && !CoordinateRegex.IsMatch(r.Latitude.Trim())) throw new ArgumentException("Latitude supports at most 4 decimal places.");
        if (!string.IsNullOrWhiteSpace(r.Longitude) && !CoordinateRegex.IsMatch(r.Longitude.Trim())) throw new ArgumentException("Longitude supports at most 4 decimal places.");
        if (!string.IsNullOrWhiteSpace(r.EmailId)) { try { _ = new MailAddress(r.EmailId.Trim()); } catch { throw new ArgumentException("Enter a valid email address."); } }
    }

    private static void ValidateForSubmission(TblUser u)
    {
        if (u.Usertype is not ("RT" or "AD" or "MD")) throw new InvalidOperationException("Select RT, AD, or MD user type.");
        if (string.IsNullOrWhiteSpace(u.Name) || string.IsNullOrWhiteSpace(u.FatherName) || string.IsNullOrWhiteSpace(u.Username) ||
            string.IsNullOrWhiteSpace(u.CompanyName) || string.IsNullOrWhiteSpace(u.EmailId) || !PhoneRegex.IsMatch(u.Phone ?? "") ||
            !PanRegex.IsMatch(u.PanCard ?? "") || !AadhaarRegex.IsMatch(u.AadharCard ?? ""))
            throw new InvalidOperationException("Complete and validate all mandatory identity fields before submitting.");
        if (!u.IsEmailVerified || !u.IsPhoneVerified || !u.IsPanVerified || !u.IsAadhaarVerified)
            throw new InvalidOperationException("Email, phone, PAN, and Aadhaar verification are required.");
        if (string.IsNullOrWhiteSpace(u.AddressLine1) || string.IsNullOrWhiteSpace(u.AddressLine2) || string.IsNullOrWhiteSpace(u.State) || string.IsNullOrWhiteSpace(u.City) ||
            !Regex.IsMatch(u.Pincode ?? "", "^[0-9]{6}$") || string.IsNullOrWhiteSpace(u.ShopAddress) ||
            string.IsNullOrWhiteSpace(u.ShopState) || string.IsNullOrWhiteSpace(u.ShopCity) || !Regex.IsMatch(u.ShipZipcode ?? "", "^[0-9]{6}$"))
            throw new InvalidOperationException("Complete all mandatory residential and shop address fields.");
        if (string.IsNullOrWhiteSpace(u.Lat) || !CoordinateRegex.IsMatch(u.Lat) || string.IsNullOrWhiteSpace(u.Longitute) || !CoordinateRegex.IsMatch(u.Longitute))
            throw new InvalidOperationException("Enter valid latitude and longitude with at most 4 decimal places.");
        if (!decimal.TryParse(u.Lat, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var latitude) || latitude is < -90 or > 90 ||
            !decimal.TryParse(u.Longitute, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var longitude) || longitude is < -180 or > 180)
            throw new InvalidOperationException("Latitude must be between -90 and 90 and longitude between -180 and 180.");
        if (!u.CommissionPlanId.HasValue || u.CommissionPlanId <= 0) throw new InvalidOperationException("Select a commission plan.");
        if (string.IsNullOrWhiteSpace(u.Wlid)) throw new InvalidOperationException("A mapped White Label user is required.");
    }

    private void ApplyConcurrencyToken(TblUser user, string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        byte[] bytes; try { bytes = Convert.FromBase64String(token); } catch { throw new ArgumentException("Invalid record version."); }
        _db.Entry(user).Property(x => x.RowVersion).OriginalValue = bytes;
    }
    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static string? Upper(string? v) => Clean(v)?.ToUpperInvariant();
    private static string? Lower(string? v) => Clean(v)?.ToLowerInvariant();
    private static string? Digits(string? v) => string.IsNullOrWhiteSpace(v) ? null : new string(v.Where(char.IsDigit).ToArray());
    private static string NormalizePhone(string? v) { var d = Digits(v) ?? ""; return d.Length > 10 ? d[^10..] : d; }
    private static void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
    { if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date) throw new ArgumentException("From Date cannot be later than To Date."); }
    private static string? Truncate(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : v.Trim()[..Math.Min(v.Trim().Length, max)];
    private static bool MatchesSignature(string extension, ReadOnlySpan<byte> bytes) => extension switch
    {
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        ".png" => bytes.Length >= 8 && bytes.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        ".pdf" => bytes.Length >= 5 && bytes[..5].SequenceEqual("%PDF-"u8),
        _ => false
    };
    private static string MaskAadhaar(string? value) => string.IsNullOrWhiteSpace(value) || value.Length < 4 ? string.Empty : $"XXXXXXXX{value[^4..]}";
}
