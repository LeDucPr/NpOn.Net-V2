using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.KafkaExtCm.Configs;
using Common.Infrastructures.NpOn.KafkaExtCm.Senders;
using Common.Infrastructures.NpOn.KafkaExtCm.Topics;
using Confluent.Kafka;

namespace Common.Applications.ApplicationsExtensions.NpOn.KafkaAppExtUse;

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddKafka(this IServiceCollection services,
        bool? isUseKafka = null, string? kafkaConnectionString = null, string? topicName = null,
        string? saslUsername = null, string? saslPassword = null)
    {
        isUseKafka ??= EApplicationConfiguration.IsUseKafka.GetAppSettingConfig().AsDefaultBool();
        if ((bool)isUseKafka)
        {
            kafkaConnectionString ??= EApplicationConfiguration.KafkaConnection.GetAppSettingConfig().AsDefaultString();
            topicName ??= EApplicationConfiguration.KafkaTopicName.GetAppSettingConfig().AsDefaultString();
            // kafka auth
            saslUsername ??= EApplicationConfiguration.SaslUsername.GetAppSettingConfig().AsDefaultString();
            saslPassword ??= EApplicationConfiguration.SaslPassword.GetAppSettingConfig().AsDefaultString();

            KafkaClientConfigBuilder configBuilder = new KafkaClientConfigBuilder();
            configBuilder.SetServerUrl(kafkaConnectionString);
            if (!string.IsNullOrEmpty(saslUsername) && !string.IsNullOrEmpty(saslPassword))
                // SASL authentication (currently only this mechanism is supported =)))
                configBuilder.SetUseSasl(saslUsername, saslPassword, SaslMechanism.ScramSha256);
            KafkaTopic kafkaTopic = KafkaTopic.Create(configBuilder.Build(), topicName)
                .GetAwaiter()
                .GetResult();
            services.AddSingleton<IKafkaTopic>(kafkaTopic);
            services.AddSingleton<IKafkaProducer, KafkaProducer>();
        }


        return services;
    }
}