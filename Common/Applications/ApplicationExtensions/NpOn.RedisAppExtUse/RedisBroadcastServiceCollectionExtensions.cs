using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;

namespace Common.Applications.ApplicationsExtensions.NpOn.RedisAppExtUse;

public static class RedisBroadcastServiceCollectionExtensions
{
    public static IServiceCollection AddRedisBroadcast(this IServiceCollection services,
        string? connectionString = null,
        params Type[]? handlerTypes)
    {
        if (handlerTypes != null)
        {
            foreach (var type in handlerTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(BaseRedisBroadcastHandler), provider => provider.GetRequiredService(type));
            }
        }

        services.AddSingleton<IRedisBroadcastFactoryWrapper, RedisBroadcastFactoryWrapper>(provider =>
        {
            connectionString ??= EApplicationConfiguration.RedisConnectString.GetAppSettingConfig().AsDefaultString();
            INpOnConnectOption connectOption = new RedisConnectOption().SetConnectionString(connectionString);
            var factoryWrapper = new RedisBroadcastFactoryWrapper(connectOption);

            var handlers = provider.GetServices<BaseRedisBroadcastHandler>().ToArray();
            foreach (var handler in handlers)
                factoryWrapper += handler;

            // Always build factory even when HandlerCount == 0 so publisher-only nodes initialize PublishAsync.
            if (factoryWrapper != null && !factoryWrapper.BuildFactory(out string? errorString) && !string.IsNullOrEmpty(errorString))
                Console.WriteLine(errorString);

            return factoryWrapper!;
        });

        return services;
    }
}
