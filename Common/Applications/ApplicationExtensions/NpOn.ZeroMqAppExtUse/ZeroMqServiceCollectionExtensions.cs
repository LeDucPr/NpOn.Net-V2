using System;
using System.Linq;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceCollectionExtensions
{
    private static bool CombineConnectionStringInProc(out string? connectionString)
    {
        connectionString = null;
        int hostPort = EApplicationConfiguration.HostPort.GetAppSettingConfig().AsDefaultInt();
        if (hostPort == 0)
        {
#if DEBUG
            return false;
#endif
            throw new ArgumentException("HostPort is required to configuration InProc identifier");
        }

        string hostDomain = EApplicationConfiguration.HostDomain.GetAppSettingConfig().AsEmptyString();
        int index = hostDomain.IndexOf("://", StringComparison.Ordinal);
        if (index == -1)
        {
#if DEBUG
            return false;
#endif
            throw new ArgumentException("HostDomain is invalid format. Missing '://' protocol separator.");
        }

        string domainPart = hostDomain.Substring(index); // lấy từ "://" 
        connectionString = $"inproc{domainPart}-{hostPort}";
        return true;
    }

    public static IServiceCollection AddZeroMqTwoWay(this IServiceCollection services,
        string? connectionString = null,
        params Type[]? handlerTypes)
    {
        if (!CombineConnectionStringInProc(out connectionString))
            return services;
        if (handlerTypes != null)
        {
            foreach (var type in handlerTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(BaseZeroMqTwoWayHandler), provider => provider.GetRequiredService(type));
            }
        }

        services.AddSingleton<IZeroMqTwoWayFactoryWrapper, ZeroMqTwoWayFactoryWrapper>(provider =>
        {
            var connectOption = new ZeroMqConnectOption();
            connectOption.SetConnectionString(connectionString!);

            ZeroMqTwoWayFactoryWrapper? factoryWrapper = new ZeroMqTwoWayFactoryWrapper(connectOption);

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