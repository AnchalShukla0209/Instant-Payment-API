using System.Security.Claims;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using InstantPay.Infrastructure.Sql.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstantPay.API.Controller;

[ApiController]
[Authorize(Roles = "SalesTeam")]
[Route("api/v1/sales-team/onboardings")]
public sealed class SalesTeamOnboardingController : ControllerBase
{
    private readonly ISalesTeamOnboardingService _service;
    private readonly IClientUserVerificationService _verificationService;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public SalesTeamOnboardingController(ISalesTeamOnboardingService service, IClientUserVerificationService verificationService, AppDbContext db, IWebHostEnvironment environment)
    {
        _service = service;
        _verificationService = verificationService;
        _db = db;
        _environment = environment;
    }

    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft([FromBody] SaveOnboardingDraftRequest request, CancellationToken ct) =>
        await Execute(() => _service.SaveDraftAsync(request, UserId(), Ip(), Request.Headers.UserAgent, ct));

    [HttpGet("hierarchy-context")]
    public async Task<IActionResult> HierarchyContext(CancellationToken ct) => await Execute(async () =>
    {
        var mappedWlId = await _db.TblUsers.AsNoTracking()
            .Where(x => x.Id == UserId() && x.Usertype == "ST" && x.Status == "Active")
            .Select(x => x.Wlid)
            .SingleOrDefaultAsync(ct);
        if (!int.TryParse(mappedWlId, out var wlUserId) || wlUserId <= 0)
            throw new InvalidOperationException("Your Sales Team account is not mapped to a White Label user. Contact SuperAdmin.");

        var wlUser = await _db.TblWlUsers.AsNoTracking()
            .Where(x => x.Id == wlUserId && x.Status == "Active")
            .Select(x => new
            {
                id = x.Id,
                name = (x.CompanyName ?? x.UserName ?? string.Empty) + "-" + (x.Phone ?? string.Empty),
                username = x.UserName ?? string.Empty,
                phone = x.Phone ?? string.Empty,
                userType = "WL"
            })
            .SingleOrDefaultAsync(ct);
        return wlUser ?? throw new InvalidOperationException("Your mapped White Label user is not active. Contact SuperAdmin.");
    });

    [HttpGet("resume-by-phone")]
    public async Task<IActionResult> ResumeByPhone([FromQuery] string phone, CancellationToken ct) =>
        await Execute(() => _service.FindOwnedDraftByPhoneAsync(phone, UserId(), ct));

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OnboardingListQuery query, CancellationToken ct) =>
        await Execute(() => _service.GetOwnedListAsync(query, UserId(), ct));

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> Detail(int userId, CancellationToken ct) =>
        await Execute(() => _service.GetOwnedDetailAsync(userId, UserId(), ct));

    [HttpPost("{userId:int}/submit")]
    public async Task<IActionResult> Submit(int userId, [FromBody] SubmitOnboardingRequest request, CancellationToken ct) =>
        await Execute(() => _service.SubmitAsync(userId, request.RowVersion, UserId(), Ip(), Request.Headers.UserAgent, ct));

    [HttpPost("{userId:int}/documents/{documentType}")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(int userId, string documentType, IFormFile file, [FromForm] string? correctionRemarks, CancellationToken ct) =>
        await Execute(() => _service.UploadDocumentAsync(userId, documentType, file, correctionRemarks, UserId(), ct));

    [HttpPost("send-phone-otp")]
    public Task<IActionResult> SendPhoneOtp([FromBody] SendClientUserOtpRequest request, CancellationToken ct) =>
        ExecuteVerification(request.ClientId, () => _verificationService.SendPhoneOtpAsync(request.Value), ct);

    [HttpPost("send-email-otp")]
    public Task<IActionResult> SendEmailOtp([FromBody] SendClientUserOtpRequest request, CancellationToken ct) =>
        ExecuteVerification(request.ClientId, () => _verificationService.SendEmailOtpAsync(request.Value), ct);

    [HttpPost("verify-otp")]
    public Task<IActionResult> VerifyOtp([FromBody] VerifyClientUserOtpRequest request, CancellationToken ct) =>
        ExecuteVerification(request.ClientId, () => _verificationService.VerifyOtpAsync(request), ct);

    [HttpPost("verify-pan")]
    public Task<IActionResult> VerifyPan([FromBody] VerifyClientUserPanRequest request, CancellationToken ct) =>
        ExecuteVerification(request.ClientId, () => _verificationService.VerifyPanAsync(request.PanNumber, request.ClientId), ct);

    [HttpPost("verify-aadhaar")]
    public Task<IActionResult> VerifyAadhaar([FromBody] VerifyClientUserAadhaarRequest request, CancellationToken ct) =>
        ExecuteVerification(request.ClientId, () => _verificationService.VerifyAadhaarAsync(request.AadharNumber, request.ClientId), ct);

    [HttpGet("{userId:int}/documents/{documentId:long}/file")]
    public async Task<IActionResult> DocumentFile(int userId, long documentId, CancellationToken ct)
    {
        if (!await IsOwnedAsync(userId, ct)) return Forbid();
        var document = await _db.TblUserOnboardingDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UserId == userId, ct);
        return document == null ? NotFound() : SecureFile(document.CurrentFilePath);
    }

    [HttpGet("{userId:int}/document-versions/{versionId:long}/file")]
    public async Task<IActionResult> DocumentVersionFile(int userId, long versionId, CancellationToken ct)
    {
        if (!await IsOwnedAsync(userId, ct)) return Forbid();
        var version = await _db.TblUserOnboardingDocumentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.UserId == userId, ct);
        return version == null ? NotFound() : SecureFile(version.FilePath);
    }

    private int UserId() => int.Parse(User.FindFirstValue("userid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private Task<bool> IsOwnedAsync(int userId, CancellationToken ct) => _db.TblUsers.AsNoTracking().AnyAsync(x => x.Id == userId && x.Stid == UserId().ToString(), ct);
    private IActionResult SecureFile(string relativePath)
    {
        var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath)) return NotFound();
        var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
    private async Task<IActionResult> ExecuteVerification<T>(int userId, Func<Task<T>> action, CancellationToken ct)
    {
        if (userId <= 0 || !await _db.TblUsers.AsNoTracking().AnyAsync(x => x.Id == userId && x.Stid == UserId().ToString()
            && (x.OnboardingStatus == InstantPay.SharedKernel.Enums.OnboardingStatuses.Draft || x.OnboardingStatus == InstantPay.SharedKernel.Enums.OnboardingStatuses.Rejected), ct))
            return Forbid();
        return await Execute(action);
    }
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { success = false, message = "This onboarding was updated elsewhere. Reload and try again." }); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return BadRequest(new { success = false, message = ex.Message }); }
    }
}
