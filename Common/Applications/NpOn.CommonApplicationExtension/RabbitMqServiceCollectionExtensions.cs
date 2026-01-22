using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;

namespace NpOn.CommonApplicationExtension;

public static class RabbitMqServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMq(this IServiceCollection services,
        bool? isUseRabbitMq = null, string? rabbitMqConnectString = null, string? exchangeName = null)
    {
        isUseRabbitMq ??= EApplicationConfiguration.IsUseRabbitMq.GetAppSettingConfig().AsDefaultBool();
        if ((bool)isUseRabbitMq)
        {
            rabbitMqConnectString ??=
                EApplicationConfiguration.RabbitMqConnection.GetAppSettingConfig().AsDefaultString();
            exchangeName ??= EApplicationConfiguration.RabbitMqExchangeName.GetAppSettingConfig().AsDefaultString();
            RabbitMqConnection rabbitMqConnection = new RabbitMqConnection(rabbitMqConnectString, exchangeName);
            // Connection and Producer of RabbitMQ must be Singleton to keep TCP connection
            services.AddSingleton<IRabbitMqConnection>(rabbitMqConnection);
            services.AddSingleton<IRabbitMqProducer, RabbitMqProducer>();
        }

        return services;
    }
}