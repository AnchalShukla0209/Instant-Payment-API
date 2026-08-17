using System.Security.Claims;
using InstantPay.Application.DTOs;
using InstantPay.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InstantPay.Infrastructure.Sql.Entities;

namespace InstantPay.API.Controller;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/v1/admin/onboardings")]
public sealed class AdminOnboardingController : ControllerBase
{
    private readonly IAdminOnboardingService _service;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;
    public AdminOnboardingController(IAdminOnboardingService service, AppDbContext db, IWebHostEnvironment environment)
    { _service = service; _db = db; _environment = environment; }
    [HttpGet] public async Task<IActionResult> List([FromQuery] AdminOnboardingListQuery q, CancellationToken ct) => await Execute(() => _service.GetListAsync(q, ct));
    [HttpGet("{id:int}")] public async Task<IActionResult> Detail(int id, CancellationToken ct) => await Execute(() => _service.GetDetailAsync(id, ct));
    [HttpPut("{id:int}/fields/{reviewId:long}")] public async Task<IActionResult> Field(int id, long reviewId, ReviewDecisionRequest r, CancellationToken ct) => await Execute(() => _service.ReviewFieldAsync(id, reviewId, r, AdminId(), Ip(), Request.Headers.UserAgent, ct));
    [HttpPut("{id:int}/documents/{documentId:long}")] public async Task<IActionResult> Document(int id, long documentId, ReviewDecisionRequest r, CancellationToken ct) => await Execute(() => _service.ReviewDocumentAsync(id, documentId, r, AdminId(), Ip(), Request.Headers.UserAgent, ct));
    [HttpPost("{id:int}/reject")] public async Task<IActionResult> Reject(int id, FinalRejectionRequest r, CancellationToken ct) => await Execute(() => _service.RejectAsync(id, r, AdminId(), Ip(), Request.Headers.UserAgent, ct));
    [HttpPost("{id:int}/approve")] public async Task<IActionResult> Approve(int id, [FromBody] string? rowVersion, CancellationToken ct) => await Execute(() => _service.ApproveAsync(id, rowVersion, AdminId(), Ip(), Request.Headers.UserAgent, ct));
    [HttpPost("{id:int}/retry-credential-email")] public async Task<IActionResult> RetryCredentialEmail(int id, CancellationToken ct) => await Execute(() => _service.RetryCredentialEmailAsync(id, AdminId(), Ip(), Request.Headers.UserAgent, ct));
    [HttpGet("{id:int}/documents/{documentId:long}/file")]
    public async Task<IActionResult> DocumentFile(int id, long documentId, CancellationToken ct)
    {
        var document = await _db.TblUserOnboardingDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UserId == id, ct);
        if (document == null) return NotFound();
        var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
        var filePath = Path.GetFullPath(Path.Combine(webRoot, document.CurrentFilePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath)) return NotFound();
        var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:int}/document-versions/{versionId:long}/file")]
    public async Task<IActionResult> DocumentVersionFile(int id, long versionId, CancellationToken ct)
    {
        var version = await _db.TblUserOnboardingDocumentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId && x.UserId == id, ct);
        if (version == null) return NotFound();
        return SecureFile(version.FilePath);
    }

    private IActionResult SecureFile(string relativePath)
    {
        var webRoot = Path.GetFullPath(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"));
        var filePath = Path.GetFullPath(Path.Combine(webRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!filePath.StartsWith(webRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(filePath)) return NotFound();
        var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", _ => "image/jpeg" };
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
    private int AdminId() => int.Parse(User.FindFirstValue("userid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip() => HttpContext.Connection.RemoteIpAddress?.ToString();
    private async Task<IActionResult> Execute<T>(Func<Task<T>> action) { try { return Ok(new { success = true, data = await action() }); } catch (KeyNotFoundException e) { return NotFound(new { success = false, message = e.Message }); } catch (DbUpdateConcurrencyException) { return Conflict(new { success = false, message = "Record changed; reload and retry." }); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return BadRequest(new { success = false, message = e.Message }); } }
}
