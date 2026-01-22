using Common.Applications.NpOn.CommonRestApplication;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Controllers.NpOn.SSO.Controllers;

namespace Controllers.NpOn.SSO;

public sealed class Program : RestCommonProgram
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
        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        services.AddControllers();
        services.AddControllers()
            .AddApplicationPart(typeof(RestCommonProgram).Assembly); // NpOn.CommonRest(Api)Application

        return Task.CompletedTask;
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        if (EApplicationConfiguration.IsUseMiddlewareLogger.GetAppSettingConfig().AsDefaultBool())
        {
            // app.UseRequestResponseLogging();
        }


        // app.UseTokenValidation();
        // app.UsePermissionValidation();

        return Task.CompletedTask;
    }
}