using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using MicroServices.General.Contract.GeneralServiceContract.ReadModels;
using MicroServices.General.Contract.NpOn.GeneralServiceContract.Queries;
using MicroServices.General.Service.NpOn.GeneralService.Services;
using MicroServices.General.Service.NpOn.IGeneralService;
// using Microsoft.AspNetCore.Server.Kestrel.Core;
using NpOn.AddGrpcAppExtUse;
using NpOn.CommonGrpcCall;

namespace MicroServices.General.Service.NpOn.GeneralService;

public sealed class Program : HttpCommonProgram
{
    protected override bool UseControllers => false;

    private Program(string[] args) : base(args)
    {
    }

    public static async Task Main(string[] args)
    {
        // Allow HTTP/2 over insecure (http) connection for Client
        Program program = new Program(args);
        await program.RunAsync();
    }

    protected override Task ConfigureServices(IServiceCollection services)
    {
        // call load balancing services 
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
            services
                .AddDefaultKestrelListenConfig()
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.CallToInternalServer))
                .AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl);

        // Register ObjectPoolStore and pre-allocate PostgresResultSetWrapper
        IObjectPoolStore store = new ObjectPoolStore();
        store.PreAllocate(
            () => new Common.Infrastructures.NpOn.PostgresExtCm.Results.PostgresResultSetWrapper(),
            EApplicationConfiguration.PostgresConnectionNumber.GetAppSettingConfig().AsDefaultInt()
        );
        services.AddSingleton(store);
        services.AddPostgres(poolStore: store);

        services.AddSingleton<IWrapperCacheStore<TblFldExecution, List<TblFldRModel>>>(_ =>
            new WrapperCacheStore<TblFldExecution, List<TblFldRModel>>()
        );


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