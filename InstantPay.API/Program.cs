using InstantPay.API.Middleware;

using InstantPay.Application.Factory;

using InstantPay.Application.IFactory;

using InstantPay.Application.Interfaces;

using InstantPay.Application.Interfaces.A2Z;

using InstantPay.Application.Interfaces.MoneyTransfer.Castler;

using InstantPay.Application.Interfaces.PAN;
using InstantPay.Application.Interfaces.Aadhaar;

using InstantPay.Application.Interfaces.RazorPay;

using InstantPay.Application.Interfaces.SMS;

using InstantPay.Application.Repositry;

using InstantPay.Application.Services;

using InstantPay.Application.Services.A2Z;

using InstantPay.Application.Services.MoneyTransfer;

using InstantPay.Application.Services.PAN;
using InstantPay.Application.Services.Aadhaar;

using InstantPay.Application.Services.RazorPay;

using InstantPay.Application.Services.SMS;

using InstantPay.Application.Interfaces.FinoAeps;

using InstantPay.Application.Services.FinoAeps;

using InstantPay.Infrastructure.Mongo;

using InstantPay.Infrastructure.Security;

using InstantPay.Infrastructure.Sql;

using InstantPay.Infrastructure.Sql.Entities;

using InstantPay.SharedKernel.AppSettingsConfiguration;

using InstantPay.SharedKernel.Entity;

using InstantPay.SharedKernel.Entity.CastlerConfigDTO;

using InstantPay.SharedKernel.Entity.AeronpayConfigDTO;

using InstantPay.SharedKernel.Entity.FinzepConfigDTO;

using InstantPay.SharedKernel.Entity.RechargeKitConfigDTO;
using InstantPay.SharedKernel.Entity.TramoConfigDTO;
using InstantPay.Application.Interfaces.MoneyTransfer.Tramo;
using InstantPay.Application.Interfaces.MoneyTransfer.RBL;
using InstantPay.Application.Interfaces.RBL;
using InstantPay.Application.Services.RBL;
using InstantPay.SharedKernel.Entity.RblConfigDTO;

using InstantPay.Application.Interfaces.MoneyTransfer.RechargeKit;

using InstantPay.SharedKernel.Entity.NIFIConfigDTO;

using InstantPay.Application.Interfaces.MoneyTransfer.AeronPay;

using InstantPay.Application.Interfaces.MoneyTransfer.Finzep;

using InstantPay.Application.Interfaces.MoneyTransfer.NIFI;

using InstantPay.Application.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;

using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Tokens;

using Microsoft.OpenApi.Models;

using System.Net;

using System.Net.Sockets;

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Net.Security;

using System.Text;
using System.Threading.RateLimiting;



System.Net.ServicePointManager.Expect100Continue = false;

System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddNewtonsoftJson();

var configuration = builder.Configuration;

string dbProvider = configuration["DatabaseProvider"] ?? "Sql";

if (dbProvider == "Mongo")

{



    builder.Services.Configure<MongoDbSettings>(configuration.GetSection("ConnectionStrings:Mongo"));



}

else

{

    builder.Services.AddDbContext<AppDbContext>(options =>

        options.UseSqlServer(configuration.GetConnectionString("Sql"),
            sqlOptions => sqlOptions.CommandTimeout(120)));

    builder.Services.AddDbContext<BeneficiaryDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("BeneficiaryDb"),
            sqlOptions => sqlOptions.CommandTimeout(120)));

    builder.Services.AddDbContext<SenderDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("SenderDb"),
            sqlOptions => sqlOptions.CommandTimeout(120)));



}

builder.Services.AddEndpointsApiExplorer();



builder.Services.AddAuthentication(options =>

{

    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

})

.AddJwtBearer(options =>

{

    options.TokenValidationParameters = new TokenValidationParameters

    {

        ValidateIssuer = true,

        ValidateAudience = true,

        ValidateLifetime = true,

        ValidateIssuerSigningKey = true,



        ValidIssuer = builder.Configuration["Jwt:Issuer"],

        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))

    };

});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DistributorOnly", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("usertype", "AD"));
    options.AddPolicy("MasterDistributorOnly", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("usertype", "MD"));
    options.AddPolicy("SuperAdminOnly", policy =>
        policy.RequireAuthenticatedUser().RequireClaim("usertype", "SuperAdmin"));
    options.AddPolicy("PartnerDashboard", policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context =>
            context.User.HasClaim("usertype", "AD") ||
            context.User.HasClaim("usertype", "MD")));
});



builder.Services.AddSwaggerGen(options =>

{

    options.SwaggerDoc("v1", new OpenApiInfo

    {

        Title = "InstantPay API",

        Version = "v1"

    });



    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme

    {

        Description = "JWT Authorization header using the Bearer scheme.",

        Name = "Authorization",

        In = ParameterLocation.Header,

        Type = SecuritySchemeType.Http,

        Scheme = "bearer"

    });



    options.AddSecurityRequirement(new OpenApiSecurityRequirement

    {

        {

            new OpenApiSecurityScheme

            {

                Reference = new OpenApiReference

                {

                    Type = ReferenceType.SecurityScheme,

                    Id = "Bearer"

                }

            },

            new string[] {}

        }

    });

});



builder.Services.AddCors(options =>

{

    options.AddPolicy("AllowAllFrontends", policy =>

    {

        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                var host = uri.Host;
                return host.Equals("demo2.instantpayment.co.in", StringComparison.OrdinalIgnoreCase) ||
                       host.Equals("instantpayment.in", StringComparison.OrdinalIgnoreCase) ||
                       host.Equals("neqs.co.in", StringComparison.OrdinalIgnoreCase) ||
                       ((host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                         host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                         host.Equals("::1", StringComparison.OrdinalIgnoreCase)) &&
                        uri.Port == 4200);
            }
            catch { return false; }
        })

        .AllowAnyHeader()

        .AllowAnyMethod()

        .AllowCredentials();

    });

});





builder.Services.AddHttpClient("JIO", client =>

{

    client.Timeout = TimeSpan.FromMinutes(5);

    client.DefaultRequestHeaders.ExpectContinue = false;

})

.ConfigurePrimaryHttpMessageHandler(() =>

{

    return new SocketsHttpHandler

    {

        Expect100ContinueTimeout = TimeSpan.Zero,



        // 🔥 FORCE IPv4

        ConnectCallback = async (context, ct) =>

        {

            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host);

            var ipv4 = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);



            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, ct);

            return new NetworkStream(socket, ownsSocket: true);

        },



        UseProxy = false,

        AutomaticDecompression = DecompressionMethods.None

    };

});



builder.Services.AddHttpClient("iQore", client =>

{

    client.Timeout = TimeSpan.FromSeconds(60);

    client.DefaultRequestHeaders.ExpectContinue = false;

})

.ConfigurePrimaryHttpMessageHandler(() =>

{

    return new SocketsHttpHandler

    {

        Expect100ContinueTimeout = TimeSpan.Zero,

        ConnectTimeout = TimeSpan.FromSeconds(5),



        // 🔥 Force IPv4

        ConnectCallback = async (context, ct) =>

        {

            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host);

            var ipv4 = addresses.First(a => a.AddressFamily == AddressFamily.InterNetwork);



            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, ct);

            return new NetworkStream(socket, ownsSocket: true);

        },



        UseProxy = false,

        AutomaticDecompression = DecompressionMethods.None

    };

});







builder.Services.AddHttpClient<IMPlanClient, MPlanClient>(client =>

{

    client.BaseAddress = new Uri("https://www.mplan.in/api/");

    client.Timeout = TimeSpan.FromSeconds(30);

});



builder.Services.AddHttpClient<IA2ZClient, A2ZClient>(client =>

{

    client.BaseAddress = new Uri("https://partners.a2zsuvidhaa.com/api/v3/");

});



builder.Services.Configure<CastlerConfig>(

    builder.Configuration.GetSection("CastlerConfig"));



builder.Services.Configure<NIFIConfig>(

    builder.Configuration.GetSection("NIFIConfig"));



builder.Services.Configure<FinzepConfig>(

    builder.Configuration.GetSection("FinzepConfig"));



builder.Services.Configure<AeronpayConfig>(

    builder.Configuration.GetSection("AeronpayConfig"));



builder.Services.Configure<RechargeKitConfig>(

    builder.Configuration.GetSection("RechargeKitConfig"));



builder.Services.Configure<TramoConfig>(

    builder.Configuration.GetSection("TramoConfig"));

builder.Services.AddOptions<RblConfig>()
    .Bind(builder.Configuration.GetSection("RblConfig"))
    .Validate(x => Uri.TryCreate(x.PaymentUrl, UriKind.Absolute, out _), "RBL PaymentUrl must be an absolute URL")
    .Validate(x => Uri.TryCreate(x.StatementUrl, UriKind.Absolute, out _), "RBL StatementUrl must be an absolute URL")
    .Validate(x => Uri.TryCreate(x.StatementWrapperUrl, UriKind.Absolute, out _), "RBL StatementWrapperUrl must be an absolute URL")
    .Validate(x => !string.IsNullOrWhiteSpace(x.ClientId) && !string.IsNullOrWhiteSpace(x.ClientSecret), "RBL client credentials are required")
    .Validate(x => !string.IsNullOrWhiteSpace(x.Username) && !string.IsNullOrWhiteSpace(x.Password), "RBL basic-auth credentials are required")
    .Validate(x => !string.IsNullOrWhiteSpace(x.CertificatePath) && !string.IsNullOrWhiteSpace(x.CertificatePassword), "RBL certificate settings are required")
    .ValidateOnStart();




builder.Services.Configure<PanApiSettings>(

    builder.Configuration.GetSection("PanApiSettings")

);

builder.Services.Configure<AadhaarApiSettings>(

    builder.Configuration.GetSection("AadhaarApiSettings")

);



builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("distributor-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("distributor-otp", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("partner-dashboard", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.FindFirst("userid")?.Value ??
                          httpContext.Connection.RemoteIpAddress?.ToString() ??
                          "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 90,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<ILoginService, LoginService>();
builder.Services.AddScoped<IDistributorAuthService, DistributorAuthService>();

builder.Services.AddSingleton<AesEncryptionService>();

builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IPartnerDashboardService, PartnerDashboardService>();
builder.Services.AddScoped<IPartnerAccountService, PartnerAccountService>();

builder.Services.AddScoped<IOperatorReadRepository, OperatorReadRepository>();

builder.Services.AddScoped<IRechargeService, RechargeService>();

builder.Services.AddScoped<IOtpService, OtpService>();

builder.Services.AddScoped<IMasterService, MasterService>();
builder.Services.AddScoped<IUserDropdownService, UserDropdownService>();
builder.Services.AddScoped<ISalesTeamOnboardingService, SalesTeamOnboardingService>();
builder.Services.AddScoped<IAdminOnboardingService, AdminOnboardingService>();

builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IClientOperation, ClientOperation>();
builder.Services.AddScoped<IClientVerificationService, ClientVerificationService>();

builder.Services.AddScoped<IClientUserOperation, ClientUserOperation>();
builder.Services.AddScoped<IClientUserVerificationService, ClientUserVerificationService>();
builder.Services.AddScoped<IUserServiceRightService, UserServiceRightService>();

builder.Services.AddScoped<ISlabReadRepository, SlabReadRepository>();

builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IServiceService, ServicesService>();

builder.Services.AddScoped<IBankRepository, BankService>();

builder.Services.AddScoped<InstantPay.Application.Interfaces.IPaymentService, InstantPay.Application.Services.PaymentService>();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IAEPSService, AEPSService>();

builder.Services.AddScoped<IJPBBalanceEnquiry, JPBBalanceEnquiry>();

builder.Services.AddScoped<IJPBMiniStatement, JPBMiniStatement>();

builder.Services.AddScoped<IJPPCashWithdrawal, JPPCashWithdrawal>();

builder.Services.AddScoped<IJPBSendNPCIOtp, JPBSendNPCIOtpService>();

builder.Services.AddScoped<IJPBCashDeposit, JPBCashDeposit>();

// ── FINO AEPS ────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("FINO", client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
    client.DefaultRequestHeaders.ExpectContinue = false;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    Expect100ContinueTimeout = TimeSpan.Zero,
    ConnectTimeout           = TimeSpan.FromSeconds(15),
    PooledConnectionLifetime = TimeSpan.FromMinutes(1),
    PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
    UseProxy                 = false,
    AutomaticDecompression   = DecompressionMethods.None,
    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
    {
        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
    }
});

builder.Services.AddScoped<IFinoAepsApiClient,         FinoAepsApiClient>();
builder.Services.AddScoped<IFinoAepsTransactionService, FinoAepsTransactionService>();
builder.Services.AddScoped<IFinoAepsWalletService,      FinoAepsWalletService>();
builder.Services.AddScoped<IFinoAepsCommissionService,  FinoAepsCommissionService>();
builder.Services.AddScoped<IFinoAepsDailyLoginCheckService, FinoAepsDailyLoginCheckService>();
builder.Services.AddScoped<IFinoMerchantEkycService,    FinoMerchantEkycService>();
builder.Services.AddScoped<IFINOBalanceEnquiryService,  FINOBalanceEnquiryService>();
builder.Services.AddScoped<IFINOCashWithdrawalService,  FINOCashWithdrawalService>();
builder.Services.AddScoped<IFINOMiniStatementService,   FINOMiniStatementService>();
builder.Services.AddScoped<IFINOCashDepositService,     FINOCashDepositService>();
builder.Services.AddScoped<IFINOAadharPayService,       FINOAadharPayService>();
builder.Services.AddScoped<IFINODailyLoginService,      FINODailyLoginService>();
builder.Services.AddScoped<IFINORegistrationService,    FINORegistrationService>();
builder.Services.AddScoped<IFINOMerchantAuthService,    FINOMerchantAuthService>();
builder.Services.AddScoped<IFINONpciOtpService,         FINONpciOtpService>();
builder.Services.AddScoped<IFinoAepsService,            FinoAepsService>();
// ─────────────────────────────────────────────────────────────────────────────

builder.Services.AddScoped<IInsuranceInfoService, InsuranceInfoService>();

builder.Services.AddScoped<IBillInfoService, BillInfoService>();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

builder.Services.AddScoped<IRechargeApiProviderService, RechargeApiProviderService>();

builder.Services.AddScoped<IInstantPayLogService, InstantPayLogService>();

builder.Services.AddScoped<IWalletRepository, WalletRepositry>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<ICommissionService, CommissionService>();

builder.Services.AddScoped<ISettlementService, SettlementService>();

builder.Services.AddScoped<IAeronpayPayoutService, AeronpayPayoutService>();

builder.Services.AddScoped<IWebsiteInfoService, WebsiteInfoService>();

builder.Services.AddScoped<IAppReleaseService>(provider =>
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    var ctx = provider.GetRequiredService<AppDbContext>();
    var logger = provider.GetRequiredService<ILogger<AppReleaseService>>();
    return new AppReleaseService(ctx, logger, env.WebRootPath);
});

builder.Services.AddScoped<IPlanDetailService, PlanDetailService>();
builder.Services.AddScoped<ICommissionPlanService, CommissionPlanService>();
builder.Services.AddScoped<IAPICodeService, APICodeService>();
builder.Services.AddScoped<IBeneficiaryService, BeneficiaryService>();
builder.Services.AddScoped<ISenderService, SenderService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIOtpService, InstantPay.Application.Services.PPI.PPIOtpService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIBeneficiaryService, InstantPay.Application.Services.PPI.PPIBeneficiaryService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIAadharService, InstantPay.Application.Services.PPI.PPIAadharService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIWalletService, InstantPay.Application.Services.PPI.PPIWalletService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIFundTransferService, InstantPay.Application.Services.PPI.PPIFundTransferService>();
builder.Services.AddScoped<InstantPay.Application.Interfaces.PPI.IPPIMoneyTransferService, InstantPay.Application.Services.PPI.PPIMoneyTransferService>();

builder.Services.AddHttpClient<ISmsService, SmsService>()

    .ConfigurePrimaryHttpMessageHandler(() =>

    {

        return new SocketsHttpHandler

        {

            ConnectCallback = async (context, cancellationToken) =>

            {

                var addresses = await Dns.GetHostEntryAsync(context.DnsEndPoint.Host);



                var ipv4 = addresses.AddressList

                    .First(x => x.AddressFamily == AddressFamily.InterNetwork);



                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);



                await socket.ConnectAsync(ipv4, context.DnsEndPoint.Port, cancellationToken);



                return new NetworkStream(socket, ownsSocket: true);

            }

        };

    });

builder.Services.AddHttpClient<ICastlerAuthService, CastlerAuthService>();

builder.Services.AddScoped<ICastlerDmtService, CastlerDmtService>();

builder.Services.AddScoped<INifiDmtService, NifiDmtService>();

builder.Services.AddScoped<IFinzepDmtService, FinzepDmtService>();

builder.Services.AddScoped<IAeronpayDmtService, AeronpayDmtService>();

builder.Services.AddScoped<IRechargeKitDmtService, RechargeKitDmtService>();

builder.Services.AddScoped<ITramoUpiDmtService, TramoUpiDmtService>();

builder.Services.AddHttpClient("RBL", client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
    client.DefaultRequestHeaders.ExpectContinue = false;
})
.ConfigurePrimaryHttpMessageHandler(serviceProvider =>
{
    var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<RblConfig>>().Value;
    var environment = serviceProvider.GetRequiredService<IWebHostEnvironment>();
    var configuredPath = settings.CertificatePath.Replace('/', Path.DirectorySeparatorChar);
    var relativeToWebRoot = configuredPath.StartsWith($"wwwroot{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        ? configuredPath[("wwwroot".Length + 1)..]
        : configuredPath;

    var certificateCandidates = Path.IsPathRooted(configuredPath)
        ? new[] { configuredPath }
        : new[]
        {
            Path.Combine(environment.ContentRootPath, configuredPath),
            Path.Combine(environment.ContentRootPath, relativeToWebRoot),
            Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), relativeToWebRoot),
            Path.Combine(AppContext.BaseDirectory, configuredPath),
            Path.Combine(AppContext.BaseDirectory, relativeToWebRoot)
        };
    var path = certificateCandidates.FirstOrDefault(File.Exists);
    if (path == null)
        throw new FileNotFoundException(
            $"RBL client certificate was not found. Checked: {string.Join("; ", certificateCandidates.Distinct())}");

    X509Certificate2? certificate = null;
    X509Certificate2Collection? certificateBundle = null;
    CryptographicException? certificateLoadError = null;
    var storageOptions = OperatingSystem.IsWindows()
        ? new[]
        {
            // Schannel must be able to reacquire the private key during the handshake.
            // An ephemeral PFX can load successfully but then fail with
            // SEC_E_UNKNOWN_CREDENTIALS under IIS/Plesk.
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet,
            X509KeyStorageFlags.EphemeralKeySet
        }
        : new[]
        {
            X509KeyStorageFlags.EphemeralKeySet,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.PersistKeySet
        };
    foreach (var storageOption in storageOptions)
    {
        try
        {
            var loadedBundle = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                path, settings.CertificatePassword, storageOption);
            certificate = loadedBundle.OfType<X509Certificate2>().FirstOrDefault(item => item.HasPrivateKey);
            certificateBundle = loadedBundle;
            if (certificate == null)
                throw new CryptographicException("The RBL certificate bundle does not contain a private key.");
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                certificate = null;
                certificateBundle = null;
                throw new CryptographicException("The RBL certificate does not contain a private key.");
            }
            break;
        }
        catch (CryptographicException ex)
        {
            certificateLoadError = ex;
        }
    }
    if (certificate == null)
        throw new CryptographicException(
            "The RBL PFX could not be loaded with any supported key-storage mode.", certificateLoadError);

    // Preserve the full chain from the PFX. Postman imports this entire collection;
    // presenting only the leaf can be rejected by the RBL/Akamai mTLS edge.
    var clientCertificates = new X509CertificateCollection();
    clientCertificates.AddRange(certificateBundle!);
    var handler = new SocketsHttpHandler
    {
        SslOptions = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12,
            CertificateRevocationCheckMode = X509RevocationMode.Online,
            ClientCertificates = clientCertificates,
            // This PFX has only the Server Authentication EKU. HttpClientHandler's
            // automatic client-certificate selection therefore silently excludes it,
            // while Postman sends it explicitly. RBL accepts this registered cert, so
            // force selection to reproduce the working Postman mTLS handshake.
            // This is a dedicated RBL-only named client, so always return its registered
            // private-key certificate; do not depend on host-string formatting.
            LocalCertificateSelectionCallback = (_, _, _, _, _) => certificate
        }
    };
    return handler;
});
builder.Services.AddScoped<IRblDmtService, RblDmtService>();
builder.Services.AddScoped<IRblStatementService, RblStatementService>();
builder.Services.AddScoped<IRblOpenSslTransport, RblOpenSslTransport>();


builder.Services.AddHttpClient<IPanService, PanService>();
builder.Services.AddHttpClient<IAadhaarService, AadhaarService>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddScoped<IRazorpayService, RazorpayService>();

builder.Services.AddScoped<IcoreRechargeRepository>();

builder.Services.AddScoped<MroboticsRechargeRepository>();

builder.Services.AddScoped<AmbikaRechargeRepository>();

builder.Services.AddScoped<CyrusRechargeRepository>();

builder.Services.AddScoped<ApiTransactionRecoveryService>();

builder.Services.AddScoped<IAccountVerifyService, InstantPay.Application.Services.AccountVerifyService>();

//builder.Services.AddHostedService<BackgroundTransactionReconciliationService>();

builder.Services.AddScoped<IFileHandler>(provider =>

{

    var env = provider.GetRequiredService<IWebHostEnvironment>();

    return new FileHandler(env.WebRootPath);

});

builder.WebHost.ConfigureKestrel(options =>

{

    options.ConfigureHttpsDefaults(httpsOptions =>

    {

        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

    });

    options.Limits.MaxRequestBodySize = 52428800; // 50 MB

});



var app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())

{

    app.UseSwagger();

    app.UseSwaggerUI(c =>

    {

        c.SwaggerEndpoint("/swagger/v1/swagger.json", "InstantPay API v1");

    });

}



app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseHttpsRedirection();

app.UseCors("AllowAllFrontends");
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<DistributorAccessBoundaryMiddleware>();
app.UseStaticFiles();
app.UseMiddleware<SessionValidationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

