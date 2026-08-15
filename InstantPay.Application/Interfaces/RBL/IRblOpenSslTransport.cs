namespace InstantPay.Application.Interfaces.RBL;

public interface IRblOpenSslTransport
{
    Task<RblTransportResponse> PostAsync(string url, string json, CancellationToken cancellationToken);
}

public sealed record RblTransportResponse(int StatusCode, string Body);
