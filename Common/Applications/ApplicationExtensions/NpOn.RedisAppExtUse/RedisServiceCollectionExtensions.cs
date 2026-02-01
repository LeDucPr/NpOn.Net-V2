using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory;

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
}