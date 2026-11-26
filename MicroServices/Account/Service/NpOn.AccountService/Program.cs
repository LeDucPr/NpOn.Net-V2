using Common.Applications.ApplicationsExtensions.NpOn.AddGrpcAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.KafkaAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RabbitMqAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RedisAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MicroServices.Account.Service.NpOn.AccountService.KafkaConsumers;
using MicroServices.Account.Service.NpOn.AccountService.RabbitMqConsumers;
using MicroServices.Account.Service.NpOn.AccountService.Services;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using NpOn.CommonGrpcCall;
using NpOn.Protos.Account.V1;
using NpOn.Protos.Tracker.V1;

namespace MicroServices.Account.Service.NpOn.AccountService;

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
                .AddConnectService(new GeneralProtoClientResolver(), null, EUrlConfiguration.GeneralServiceUrl)
                .AddProtoConnectService<TrackerLogProtoService.TrackerLogProtoServiceClient>(EUrlConfiguration.TrackerServiceUrl)
                .AddProtoConnectService(EUrlConfiguration.AccountServiceUrl, (s, url) =>
                {
                    s.RegisterSchemaFirstGrpcClientLoadBalancing<AccountInfoProtoService.AccountInfoProtoServiceClient>(url);
                    s.RegisterSchemaFirstGrpcClientLoadBalancing<AuthenticationProtoService.AuthenticationProtoServiceClient>(url);
                    s.RegisterSchemaFirstGrpcClientLoadBalancing<AccountPermissionProtoService.AccountPermissionProtoServiceClient>(url);
                    s.RegisterSchemaFirstGrpcClientLoadBalancing<AccountGroupProtoService.AccountGroupProtoServiceClient>(url);
                });
        }

        // Register ObjectPoolStore and pre-allocate PostgresResultSetWrapper
        IObjectPoolStore store = new ObjectPoolStore().PreAllocate(
            () => new NpOnWrapperResult(),
            100
        );
        services.AddSingleton<IObjectPoolStore>(_ => store);
        services.AddSingleton(store);
        services
            .AddPostgres(poolStore: store)
            .AddRedis()
            .AddRedisBroadcast();

        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        // rabbitMq
        bool isUseRabbitMq = EApplicationConfiguration.IsUseRabbitMq.GetAppSettingConfig().AsDefaultBool();
        if (isUseRabbitMq)
        {
            services.AddRabbitMq(); // rabbitMq
            services.AddTransient<AccountSaveLoginRabbitMqConsumer>()
                .AddHostedService<ConsumerHostedService<AccountSaveLoginRabbitMqConsumer>>();
            services.AddTransient<AccountSaveLogoutRabbitMqConsumer>()
                .AddHostedService<ConsumerHostedService<AccountSaveLogoutRabbitMqConsumer>>();
        }

        // kafka
        bool isUseKafka = EApplicationConfiguration.IsUseKafka.GetAppSettingConfig().AsDefaultBool();
        if (isUseKafka)
        {
            services.AddKafka(); // kafka
            services.AddTransient<AccountSaveLoginKafkaConsumer>()
                .AddHostedService<ConsumerHostedService<AccountSaveLoginKafkaConsumer>>();
        }

        // zeromq 
        services.AddZeroMqTwoWay(connectionString: null);
        services.AddZeroMqMultiClients([
                EUrlConfiguration.TrackerServiceUrl
            ]
            /*, handler*/);

        // Add Service
        services.AddTransient<IAccountInfoService, AccountInfoService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IAccountPermissionService, AccountPermissionService>();
        services.AddTransient<IAccountGroupService, AccountGroupService>();

        // Add Repository
        services.AddTransient<IAccountInfoStorageAdapter, AccountInfoStorageAdapter>();
        services.AddTransient<IAccountPermissionStorageAdapter, AccountPermissionStorageAdapter>();
        services.AddTransient<IAuthenticationStorageAdapter, AuthenticationStorageAdapter>();
        services.AddTransient<IAccountTokenAndPermissionRedisRepository, AccountTokenAndPermissionRedisRepository>();
        services.AddTransient<IAccountGroupStorageAdapter, AccountGroupStorageAdapter>();

        return Task.CompletedTask;
    }

    // protected override void ConfigureBasePipeline(WebApplication app)
    // { app.MapGet("/", () => "NpOn.AccountService"); base.ConfigureBasePipeline(app); }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service (Schema-First .proto + Code-First coexistence)
        app.MapGrpcService<AccountInfoProtoGrpcService>();
        app.MapGrpcService<AuthenticationProtoGrpcService>();
        app.MapGrpcService<AccountPermissionProtoGrpcService>();
        app.MapGrpcService<AccountGroupProtoGrpcService>();
        app.MapGrpcService<AccountInfoService>();
        app.MapGrpcService<AuthenticationService>();
        app.MapGrpcService<AccountPermissionService>();
        app.MapGrpcService<AccountGroupService>();
        return Task.CompletedTask;
    }
}