using System.Diagnostics;
using InstantPay.Application.Interfaces.RBL;
using InstantPay.SharedKernel.Entity.RblConfigDTO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace InstantPay.Application.Services.RBL;

public sealed class RblOpenSslTransport : IRblOpenSslTransport
{
    private readonly RblConfig _config;
    private readonly IHostingEnvironment _environment;
    private readonly ILogger<RblOpenSslTransport> _logger;

    public RblOpenSslTransport(IOptions<RblConfig> config, IHostingEnvironment environment,
        ILogger<RblOpenSslTransport> logger)
    {
        _config = config.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<RblTransportResponse> PostAsync(string url, string json, CancellationToken cancellationToken)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "RblTransport", "rbl-openssl-client.js");
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("The RBL OpenSSL transport script is missing.", scriptPath);

        var certificatePath = ResolveCertificatePath();
        var input = JsonConvert.SerializeObject(new
        {
            url,
            body = json,
            username = _config.Username,
            password = _config.Password,
            certificatePath,
            certificatePassword = _config.CertificatePassword,
            timeoutMs = 90000
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = string.IsNullOrWhiteSpace(_config.NodeExecutable) ? "node" : _config.NodeExecutable,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add(scriptPath);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start()) throw new InvalidOperationException("Could not start the RBL OpenSSL transport.");

        await process.StandardInput.WriteAsync(input.AsMemory(), cancellationToken);
        process.StandardInput.Close();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogError("RBL OpenSSL transport exited with {ExitCode}: {Error}", process.ExitCode, stderr);
            throw new HttpRequestException($"RBL OpenSSL transport failed: {stderr.Trim()}");
        }

        var result = JsonConvert.DeserializeObject<NodeTransportResult>(stdout)
            ?? throw new JsonException("RBL OpenSSL transport returned an empty result.");
        return new RblTransportResponse(result.StatusCode, result.Body ?? string.Empty);
    }

    private string ResolveCertificatePath()
    {
        var configured = _config.CertificatePath.Replace('/', Path.DirectorySeparatorChar);
        var webRelative = configured.StartsWith($"wwwroot{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            ? configured[("wwwroot".Length + 1)..]
            : configured;
        var candidates = Path.IsPathRooted(configured) ? new[] { configured } : new[]
        {
            Path.Combine(_environment.ContentRootPath, configured),
            Path.Combine(_environment.ContentRootPath, webRelative),
            Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), webRelative),
            Path.Combine(AppContext.BaseDirectory, configured),
            Path.Combine(AppContext.BaseDirectory, webRelative)
        };
        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("RBL certificate file was not found for the OpenSSL transport.");
    }

    private sealed class NodeTransportResult
    {
        public int StatusCode { get; set; }
        public string? Body { get; set; }
    }
}
