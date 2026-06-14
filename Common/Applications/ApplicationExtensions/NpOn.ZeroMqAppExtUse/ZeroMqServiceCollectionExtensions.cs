using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceCollectionExtensions
{
    private static bool CombineConnectionStringIpc(out string? connectionString)
    {
        connectionString = null;
        int hostPort = EApplicationConfiguration.HostPort.GetAppSettingConfig().AsDefaultInt();
        if (hostPort == 0)
        {
#if DEBUG
            return false;
#endif
            throw new ArgumentException("HostPort is required to configuration IPC identifier");
        }

        connectionString = ZeroMqIpcHelper.CombineConnectionStringIpc(hostPort);
        return true;
    }

    public static IServiceCollection AddZeroMqTwoWay(this IServiceCollection services,
        string? connectionString = null,
        params Type[]? handlerTypes)
    {
        // Gọi hàm sinh chuỗi kết nối IPC thay vì InProc
        if (!CombineConnectionStringIpc(out connectionString))
            return services;

        if (handlerTypes != null)
        {
            foreach (var type in handlerTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(BaseZeroMqTwoWayHandler), provider => provider.GetRequiredService(type));
            }
        }

        services.AddSingleton<IZeroMqTwoWayProvider, ZeroMqTwoWayProvider>(provider =>
        {
            var connectOption = new ZeroMqConnectOption();
            connectOption.SetConnectionString(connectionString!);

            ZeroMqTwoWayProvider? factoryWrapper = new ZeroMqTwoWayProvider(connectOption);

            var handlers = provider.GetServices<BaseZeroMqTwoWayHandler>().ToArray();
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