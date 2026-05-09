using Common.Applications.ApplicationsExtensions.NpOn.AddGrpcAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.General.Service.NpOn.IGeneralService;
using NpOn.CommonGrpcCall;
using Common.Applications.ApplicationsExtensions.NpOn.ClickHouseAppExtUse;
using MicroServices.Tracker.Service.NpOn.ITrackerService;
using MicroServices.Tracker.Service.NpOn.TrackerService.Services;
using NpOn.ITrackerStorageAdapter;
using NpOn.TrackerStorageAdapter;

namespace MicroServices.Tracker.Service.NpOn.TrackerService;

public sealed class Program : HttpCommonProgram
{
    protected override bool UseControllers => false;

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
                .AddDefaultKestrelListenConfig(out _)
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.InternalServer))
                .AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl)
                .AddConnectService(new AccountServiceClientResolver(), null, EUrlConfiguration.AccountServiceUrl)
                .AddConnectService(new TrackerServiceClientResolver(), null, EUrlConfiguration.TrackerServiceUrl);

        IObjectPoolStore store = new ObjectPoolStore().PreAllocate(
            () => new NpOnWrapperResult(),
            100
        );
        services.AddSingleton<IObjectPoolStore>(_ => store);
        services.AddSingleton(store);
        
        if (EApplicationConfiguration.IsUseClickhouse.GetAppSettingConfig().AsDefaultBool())
            services.AddClickHouse(poolStore: store);
        
        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }
        
        // Add Service
        services.AddTransient<ITrackerLogService, TrackerLogService>();
        
        // Add StorageAdapter
        services.AddTransient<ISystemLogStorageAdapter, SystemLogStorageAdapter>();

        return Task.CompletedTask;
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service
        app.MapGrpcService<TrackerLogService>();
        
        // // Initialize ClickHouse Schema (static func)
        // Task.Run(async () => {
        //     using var scope = app.Services.CreateScope();
        //     await Database.ClickHouseLogSchema.InitializeAsync(scope.ServiceProvider);
        // });

        return Task.CompletedTask;
    }
}