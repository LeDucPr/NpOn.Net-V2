using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.ApplicationsExtensions.NpOn.RabbitMqAppExtUse;
using Common.Applications.NpOn.CommonApplication.Extensions;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Applications.NpOn.CommonHttpApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using MicroServices.Migration.Service.NpOn.MigrationService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using NpOn.AddGrpcAppExtUse;
using NpOn.CommonGrpcCall;

namespace MicroServices.Migration.Service.NpOn.MigrationService;

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
                .AddDefaultKestrelListenConfig()
                .AddGrpcDefaultMode()
                .AddScoped<GrpcHeaderConfig>(_ => new GrpcHeaderConfig(EGrpcEndUseType.CallToInternalServer));
                // .AddConnectService(new GeneralServiceClientResolver(), null, EUrlConfiguration.GeneralServiceUrl)
                // .AddConnectService(new AccountServiceClientResolver(), null, EUrlConfiguration.AccountServiceUrl);
        
        // Register ObjectPoolStore and pre-allocate PostgresResultSetWrapper
        IObjectPoolStore store = new ObjectPoolStore();
        store.PreAllocate(
            () => new NpOnWrapperResult(),
            1000
        );  
        services.AddSingleton(store);
        services
            .AddPostgres(poolStore: store);
            // .AddRedis();


        if (EApplicationConfiguration.IsStartAsync.GetAppSettingConfig().AsDefaultBool())
        {
            services.AddHostedService<HostingApp>();
        }

        // rabbitMq
        bool isUseRabbitMq = EApplicationConfiguration.IsUseRabbitMq.GetAppSettingConfig().AsDefaultBool();
        if (isUseRabbitMq)
        {
            services.AddRabbitMq(); // rabbitMq
            // services.AddTransient<AccountSaveLoginRabbitMqConsumer>()
            //     .AddHostedService<ConsumerHostedService<AccountSaveLoginRabbitMqConsumer>>();
            // services.AddTransient<AccountSaveLogoutRabbitMqConsumer>()
            //     .AddHostedService<ConsumerHostedService<AccountSaveLogoutRabbitMqConsumer>>();
        }

        // kafka
        // bool isUseKafka = EApplicationConfiguration.IsUseKafka.GetAppSettingConfig().AsDefaultBool();
        // if (isUseKafka)
        // {
        //     services.AddKafka(); // kafka
        //     services.AddTransient<AccountSaveLoginKafkaConsumer>()
        //         .AddHostedService<ConsumerHostedService<AccountSaveLoginKafkaConsumer>>();
        // }

        // Add Service
        // services.AddTransient<IAccountInfoService, AccountInfoService>();

        // Add Repository
        // services.AddTransient<IAccountInfoStorageAdapter, AccountInfoStorageAdapter>();

        return Task.CompletedTask;
    }

    // protected override void ConfigureBasePipeline(WebApplication app)
    // { app.MapGet("/", () => "NpOn.AccountService"); base.ConfigureBasePipeline(app); }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service
        // app.MapGrpcService<AccountInfoService>();
        return Task.CompletedTask;
    }
}