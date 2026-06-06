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
        params Type[]? handlerTypes) // Thêm tham số nhận danh sách Type ở đây
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
            RedisBroadcastFactoryWrapper? factoryWrapper = new RedisBroadcastFactoryWrapper(connectOption);

            var handlers = provider.GetServices<BaseRedisBroadcastHandler>().ToArray();
            if (!handlers.Any())
                return factoryWrapper;

            foreach (var handler in handlers)
                factoryWrapper += handler;

            if (!factoryWrapper!.BuildFactory(out string? errorString))
                Console.WriteLine(errorString);

            return factoryWrapper;
        });

        return services;
    }
}