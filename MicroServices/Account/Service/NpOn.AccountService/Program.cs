using Common.Applications.ApplicationsExtensions.NpOn.KafkaAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RabbitMqAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RedisAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using MicroServices.Account.Service.NpOn.AccountService.KafkaConsumers;
using MicroServices.Account.Service.NpOn.AccountService.RabbitMqConsumers;
using MicroServices.Account.Service.NpOn.AccountService.Services;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Service.NpOn.IGeneralService;
using NpOn.AddGrpcAppExtUse;
using NpOn.CommonGrpcCall;

namespace MicroServices.Account.Service.NpOn.AccountService;

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
                .AddDefaultKestrelListenConfig()
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.CallToInternalServer))
                .AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl)
                .AddConnectService(new AccountServiceClientResolver(), null, EUrlConfiguration.AccountServiceUrl);

        services
            .AddPostgres()
            .AddRedis();

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

        // Add Service
        services.AddTransient<IAccountInfoService, AccountInfoService>();
        services.AddTransient<IAuthenticationService, AuthenticationService>();
        services.AddTransient<IAccountPermissionService, AccountPermissionService>();
        services.AddTransient<IAccountGroupService, AccountGroupService>();
        services.AddTransient<IAccountMenuService, AccountMenuService>();
        services.AddTransient<IAccountMenuPermissionService, AccountMenuPermissionService>();

        // Add Repository
        services.AddTransient<IAccountInfoStorageAdapter, AccountInfoStorageAdapter>();
        services.AddTransient<IAccountPermissionStorageAdapter, AccountPermissionStorageAdapter>();
        services.AddTransient<IAuthenticationStorageAdapter, AuthenticationStorageAdapter>();
        services.AddTransient<IAccountTokenAndPermissionRedisRepository, AccountTokenAndPermissionRedisRepository>();
        services.AddTransient<IAccountGroupStorageAdapter, AccountGroupStorageAdapter>();
        services.AddTransient<IAccountMenuStorageAdapter, AccountMenuStorageAdapter>();
        services.AddTransient<IAccountMenuPermissionStorageAdapter, AccountMenuPermissionStorageAdapter>();

        return Task.CompletedTask;
    }

    // protected override void ConfigureBasePipeline(WebApplication app)
    // { app.MapGet("/", () => "NpOn.AccountService"); base.ConfigureBasePipeline(app); }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service
        app.MapGrpcService<AccountInfoService>();
        app.MapGrpcService<AuthenticationService>();
        app.MapGrpcService<AccountPermissionService>();
        app.MapGrpcService<AccountGroupService>();
        app.MapGrpcService<AccountMenuService>();
        app.MapGrpcService<AccountMenuPermissionService>();
        return Task.CompletedTask;
    }
}