using InstantPay.SharedKernel.RequestPayload.RBL;

namespace InstantPay.Application.Interfaces.RBL;

public interface IRblStatementService
{
    Task<RblStatementApiResult> GetDateRangeAsync(RblDateRangeStatementRequest request, CancellationToken cancellationToken);
    Task<RblStatementApiResult> GetPeriodAsync(RblPeriodStatementRequest request, CancellationToken cancellationToken);
}

public sealed record RblStatementApiResult(bool Success, string ResponseJson, string? ErrorMessage = null, int ErrorStatusCode = 502);
