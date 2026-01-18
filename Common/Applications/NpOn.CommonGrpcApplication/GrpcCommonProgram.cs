using Common.Applications.NpOn.CommonApplication;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.KafkaExtCm.Configs;
using Common.Infrastructures.NpOn.KafkaExtCm.Senders;
using Common.Infrastructures.NpOn.KafkaExtCm.Topics;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;
using Confluent.Kafka;
using Grpc.Net.Client.Balancer;
using ProtoBuf.Grpc.Server;

namespace NpOn.CommonGrpcApplication;

public abstract class GrpcCommonProgram(string[] args) : CommonProgram(args)
{
    protected override Task ConfigureServices(IServiceCollection services)
    {
        // common grpc
        services.AddCodeFirstGrpc(config =>
        {
            config.ResponseCompressionLevel = System.IO.Compression.CompressionLevel.NoCompression;
            config.MaxReceiveMessageSize = int.MaxValue;
            config.MaxSendMessageSize = int.MaxValue;
            //config.Interceptors.Add<>();
        });
        // services.RegisterGrpcClientLoadBalancing(); // add DI multi Services
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        int dnsRsvF = EApplicationConfiguration.DnsResolverFactory.GetAppSettingConfig().AsDefaultInt();
        services.AddSingleton<ResolverFactory>(new DnsResolverFactory(refreshInterval: TimeSpan.FromSeconds(dnsRsvF)));
        // services.AddGrpc();

        // rabbitMq
        bool isUseRabbitMq = EApplicationConfiguration.IsUseRabbitMq.GetAppSettingConfig().AsDefaultBool();
        if (isUseRabbitMq)
        {
            string rabbitCnStr = EApplicationConfiguration.RabbitMqConnection.GetAppSettingConfig().AsDefaultString();
            string exName = EApplicationConfiguration.RabbitMqExchangeName.GetAppSettingConfig().AsDefaultString();
            RabbitMqConnection rabbitMqConnection = new RabbitMqConnection(rabbitCnStr, exName);
            // Connection and Producer of RabbitMQ must be Singleton to keep TCP connection
            services.AddSingleton<IRabbitMqConnection>(rabbitMqConnection);
            services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();
        }

        // kafka
        bool isUseKafka = EApplicationConfiguration.IsUseKafka.GetAppSettingConfig().AsDefaultBool();
        if (isUseKafka)
        {
            string kafkaCnStr = EApplicationConfiguration.KafkaConnection.GetAppSettingConfig().AsDefaultString();
            string topicName = EApplicationConfiguration.KafkaTopicName.GetAppSettingConfig().AsDefaultString();
            // kafka auth
            string saslUsername = EApplicationConfiguration.SaslUsername.GetAppSettingConfig().AsDefaultString();
            string saslPassword = EApplicationConfiguration.SaslPassword.GetAppSettingConfig().AsDefaultString();

            KafkaClientConfigBuilder configBuilder = new KafkaClientConfigBuilder();
            configBuilder.SetServerUrl(kafkaCnStr);
            if (!string.IsNullOrEmpty(saslUsername) && !string.IsNullOrEmpty(saslPassword))
                // SASL authentication (currently only this mechanism is supported =)))
                configBuilder.SetUseSasl(saslUsername, saslPassword, SaslMechanism.ScramSha256);
            KafkaTopic kafkaTopic = KafkaTopic.Create(configBuilder.Build(), topicName)
                .GetAwaiter()
                .GetResult();
            services.AddSingleton<IKafkaTopic>(kafkaTopic);
            services.AddSingleton<IKafkaProducer, KafkaProducer>();
        }
        return Task.CompletedTask;
    }

    protected override void ConfigureBasePipeline(WebApplication app)
    {
        string appName = EApplicationConfiguration.AppName.GetAppSettingConfig().AsDefaultString();
        app.MapGet("/", () => $"NpOn.{appName}");
        base.ConfigureBasePipeline(app);
    }

    protected override Task ConfigurePipeline(WebApplication app)
    {
        // Add Map Grpc Service ??
        return Task.CompletedTask;
    }
}