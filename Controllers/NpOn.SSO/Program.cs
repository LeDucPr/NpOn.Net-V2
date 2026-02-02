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
using NpOn.AddGrpcAppExtUse;
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
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
            services
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.CallToInternalServer))
                .AddConnectService(new AccountServiceClientResolver(), null, EUrlConfiguration.AccountServiceUrl)
                .UseTokenValidatorDefaultMode(); // valid custom logic for yours 

        services.UseCorsDefaultMode(); // cors

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
        if (EApplicationConfiguration.IsUseMiddlewareLogger.GetAppSettingConfig().AsDefaultBool())
        {
            // app.UseRequestResponseLogging();
        }

        app.UseTokenValidation();
        app.UsePermissionValidation();

        return Task.CompletedTask;
    }
}