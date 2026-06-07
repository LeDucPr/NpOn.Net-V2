using Microsoft.Extensions.DependencyInjection;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Broadcast;
using Microsoft.Extensions.Logging;

namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceCollectionExtensions
{
    public static IServiceCollection AddNpOnZeroMqService(
        this IServiceCollection services,
        string connectionString,
        Action<DbNpOnConnectOption<ZeroMqDriver>>? configureOptions = null)
    {
        var connectOption = new ZeroMqConnectOption { ConnectionString = connectionString };
        configureOptions?.Invoke(connectOption);

        services.AddSingleton<INpOnDbDriver>(provider =>
        {
            var loggerFactory = provider.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                connectOption.Logger = loggerFactory.CreateLogger<ZeroMqDriver>();
            }
            return new ZeroMqDriver(connectOption);
        });

        services.AddSingleton<IZeroMqBroadcastService, ZeroMqBroadcastService>();
        services.AddSingleton(provider =>
        {
            var broadcastService = provider.GetRequiredService<IZeroMqBroadcastService>();
            var logger = provider.GetRequiredService<ILogger<ZeroMqBroadcastService>>();
            // Assuming a default address or getting it from configuration
            // For now, hardcoding for demonstration. This should be configurable.
            broadcastService.Start("tcp://*:5556");
            return broadcastService;
        });

        return services;
    }
}