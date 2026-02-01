using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.CommonWebApplication;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;
using MicroServices.General.Service.NpOn.GeneralService.Services;
using MicroServices.General.Service.NpOn.IGeneralService;
using NpOn.CommonGrpcCall;

namespace MicroServices.General.Service.NpOn.GeneralService;

public sealed class Program : CommonProgram
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
        // call load balancing services 
        services.AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.CallToInternalServer));
        services.AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl);
        
        // utils
        services.AddSingleton<IDbFactoryWrapper>(_ =>
        {
            string connectionString =
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            int connectionNumber = EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            IDbFactoryWrapper factoryWrapper =
                new DbFactoryWrapper(connectionString, EDb.Postgres, connectionNumber);
            return factoryWrapper;
        });

        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        services.AddTransient<IFldMasterPgService, FldMasterPgService>();

        return Task.CompletedTask;
    }

    // protected override void ConfigureBasePipeline(WebApplication app)
    // { app.MapGet("/", () => "NpOn.GeneralService"); base.ConfigureBasePipeline(app); }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        app.MapGrpcService<FldMasterPgService>();
        return Task.CompletedTask;
    }
}