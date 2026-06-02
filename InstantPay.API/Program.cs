using InstantPay.API.Middleware;

using InstantPay.Application.Factory;

using InstantPay.Application.IFactory;

using InstantPay.Application.Interfaces;

using InstantPay.Application.Interfaces.A2Z;

using InstantPay.Application.Interfaces.MoneyTransfer.Castler;

using InstantPay.Application.Interfaces.PAN;

using InstantPay.Application.Interfaces.RazorPay;

using InstantPay.Application.Interfaces.SMS;

using InstantPay.Application.Repositry;

using InstantPay.Application.Services;

using InstantPay.Application.Services.A2Z;

using InstantPay.Application.Services.MoneyTransfer;

using InstantPay.Application.Services.PAN;

using InstantPay.Application.Services.RazorPay;

using InstantPay.Application.Services.SMS;

using InstantPay.Infrastructure.Mongo;

using InstantPay.Infrastructure.Security;

using InstantPay.Infrastructure.Sql;

using InstantPay.Infrastructure.Sql.Entities;

using InstantPay.SharedKernel.AppSettingsConfiguration;

using InstantPay.SharedKernel.Entity;

using InstantPay.SharedKernel.Entity.CastlerConfigDTO;

using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.EntityFrameworkCore;

using Microsoft.IdentityModel.Tokens;

using Microsoft.OpenApi.Models;

using System.Net;

using System.Net.Sockets;

using System.Security.Authentication;

using System.Text;



System.Net.ServicePointManager.Expect100Continue = false;

System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var configuration = builder.Configuration;

string dbProvider = configuration["DatabaseProvider"] ?? "Sql";

if (dbProvider == "Mongo")

{



    builder.Services.Configure<MongoDbSettings>(configuration.GetSection("ConnectionStrings:Mongo"));



}

else

{

    builder.Services.AddDbContext<AppDbContext>(options =>

        options.UseSqlServer(configuration.GetConnectionString("Sql")));



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

        policy.WithOrigins(

            "https://demo2.instantpayment.co.in",

            "https://neqs.co.in",

            "http://localhost:4200"

        )

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

    client.Timeout = TimeSpan.FromSeconds(20);

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



builder.Services.Configure<PanApiSettings>(

    builder.Configuration.GetSection("PanApiSettings")

);



builder.Services.AddMemoryCache();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<ILoginService, LoginService>();

builder.Services.AddSingleton<AesEncryptionService>();

builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddScoped<IOperatorReadRepository, OperatorReadRepository>();

builder.Services.AddScoped<IRechargeService, RechargeService>();

builder.Services.AddScoped<IOtpService, OtpService>();

builder.Services.AddScoped<IMasterService, MasterService>();

builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IClientOperation, ClientOperation>();

builder.Services.AddScoped<IClientUserOperation, ClientUserOperation>();

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

builder.Services.AddScoped<IJPBCashDeposit, JPBCashDeposit>();

builder.Services.AddScoped<IInsuranceInfoService, InsuranceInfoService>();

builder.Services.AddScoped<IBillInfoService, BillInfoService>();

builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddHttpClient<IWhatsAppService, WhatsAppService>();

builder.Services.AddScoped<IRechargeApiProviderService, RechargeApiProviderService>();

builder.Services.AddScoped<IInstantPayLogService, InstantPayLogService>();

builder.Services.AddScoped<IWalletRepository, WalletRepositry>();

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

builder.Services.AddHttpClient<IPanService, PanService>();

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddScoped<IRazorpayService, RazorpayService>();

builder.Services.AddScoped<IcoreRechargeRepository>();

builder.Services.AddScoped<MroboticsRechargeRepository>();

builder.Services.AddScoped<AmbikaRechargeRepository>();

builder.Services.AddScoped<CyrusRechargeRepository>();

builder.Services.AddScoped<ApiTransactionRecoveryService>();

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

app.UseAuthentication();

app.UseAuthorization();

app.UseStaticFiles();

app.UseMiddleware<SessionValidationMiddleware>();

app.MapControllers();

app.Run();

