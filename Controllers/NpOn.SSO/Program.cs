using Common.Applications.ApplicationsExtensions.NpOn.AddGrpcAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Middlewares;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Controllers.NpOn.SSO.Controllers;
using MicroServices.Account.Service.NpOn.IAccountService;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NpOn.CommonGrpcCall;

namespace Controllers.NpOn.SSO;

public sealed class Program : HttpCommonProgram
{
    private Program(string[] args) : base(args)
    {
    }

    public static async Task Main(string[] args)
    {
        Program program = new Program(args);
        await program.RunAsync();
    }

    protected override Task ConfigureServices(IServiceCollection services)
    {
        HttpProtocols? protocols = null;
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
            services
                .AddDefaultKestrelListenConfig(out protocols)
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.InternalServer))
                .AddConnectService(new AccountServiceClientResolver(), null, EUrlConfiguration.AccountServiceUrl);

        services.UseCorsDefaultMode() // cors
            .UseTokenValidatorDefaultMode(); // valid custom logic for yours 

        int grpcHostPort = EApplicationConfiguration.PublicGrpcHostPort.GetAppSettingConfig().AsDefaultInt();
        if (grpcHostPort != 0 && protocols != null)
        {
            int basePort = EApplicationConfiguration.HostPort.GetAppSettingConfig().AsDefaultInt();
            HttpProtocols[] combineProtocols = [(HttpProtocols)protocols, HttpProtocols.Http1];
            services.Configure<KestrelServerOptions>(options =>
            {
                options.ListenAnyIP(basePort, // json (rest api)
                    listenOptions =>
                    {
                        listenOptions.Protocols = combineProtocols.CombineFlags();
                    });
                options.ListenAnyIP(grpcHostPort,
                    listenOptions => { listenOptions.Protocols = (HttpProtocols)protocols; });
            });
        }

        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        services.AddControllers();
        services.AddControllers()
            .AddApplicationPart(typeof(HttpCommonProgram).Assembly); // NpOn.CommonRest(Api)Application

        return Task.CompletedTask;
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        app.MapGrpcService<AccountController>();

        if (EApplicationConfiguration.IsUseMiddlewareLogger.GetAppSettingConfig().AsDefaultBool())
        {
            // app.UseRequestResponseLogging();
        }

        app.ExportProtoFileOnDev(typeof(Program).Assembly);

        app.UseTokenValidation();
        app.UsePermissionValidation();

        return Task.CompletedTask;
    }
}