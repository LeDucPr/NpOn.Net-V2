using Common.Applications.ApplicationsExtensions.NpOn.AddGrpcAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RedisAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using MicroServices.General.Service.NpOn.GeneralService.Services;
using MicroServices.General.Service.NpOn.IGeneralService;
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
        Program program = new Program(args);
        await program.RunAsync();
    }

    protected override Task ConfigureServices(IServiceCollection services)
    {
        if (EApplicationConfiguration.IsUseGrpcStandardMode.GetAppSettingConfig().AsDefaultBool())
        {
            var serviceName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
            services
                .AddDefaultKestrelListenConfig(out _)
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(sp => new GrpcHeaderConfig(
                    EGrpcEndUseType.InternalServer,
                    null,
                    sp.GetRequiredService<IInterServiceTokenService>(),
                    serviceName))
                .AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl);
        }

        // Register ObjectPoolStore and pre-allocate PostgresResultSetWrapper
        IObjectPoolStore store = new ObjectPoolStore().PreAllocate(
            () => new NpOnWrapperResult(),
            EApplicationConfiguration.PostgresConnectionNumber.GetAppSettingConfig().AsDefaultInt()
        );
        services.AddSingleton(store);
        services.AddPostgres(poolStore: store);

        if (EApplicationConfiguration.IsUseRedisCache.GetAppSettingConfig().AsDefaultBool())
        {
            services
                .AddRedis()
                .AddRedisBroadcast();
        }

        services.AddSingleton<IWrapperCacheStore<string, List<TblFldRModel>>>(_ =>
            new WrapperCacheStore<string, List<TblFldRModel>>()
        );

        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        services.AddTransient<IFldMasterService, FldMasterService>();

        return Task.CompletedTask;
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Eagerly resolve RedisBroadcastFactoryWrapper so publisher multiplexer connects at startup.
        _ = app.Services.GetService<IRedisBroadcastFactoryWrapper>();

        app.MapGrpcService<FldMasterService>();
        app.MapGrpcService<FldMasterProtoGrpcService>();
        return Task.CompletedTask;
    }
}
