using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;

namespace Common.Applications.ApplicationsExtensions.NpOn.RedisAppExtUse;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedis(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null)
    {
        services.AddSingleton<IRedisFactoryWrapper, RedisFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.RedisConnectString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??= EApplicationConfiguration.RedisConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            IRedisFactoryWrapper factoryWrapper =
                new RedisFactoryWrapper(connectionString, (int)connectionNumber, true);
            return (RedisFactoryWrapper)factoryWrapper;
        });
        return services;
    }

    public static IServiceCollection AddRedisBroadcast(this IServiceCollection services,
        string? connectionString = null,
        params BaseRedisBroadcastHandler[]? handlers)
    {
        services.AddSingleton<IRedisBroadcastFactoryWrapper, RedisBroadcastFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.RedisConnectString.GetAppSettingConfig().AsDefaultString();
            INpOnConnectOption connectOption = new RedisConnectOption()
                .SetConnectionString(connectionString);
            RedisBroadcastFactoryWrapper? factoryWrapper =
                new RedisBroadcastFactoryWrapper(connectOption);
            if (handlers is { Length: > 0 })
            {
                foreach (var handler in handlers)
                    factoryWrapper += handler;
                if (!factoryWrapper!.BuildFactory(out string? errorString))
                    Console.WriteLine(errorString);
            }
            return factoryWrapper;
        });
        return services;
    }
}