using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;


namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceMultiCollectionExtensions
{
    /// <summary>
    /// Multi ZeroMQ IPC
    /// </summary>
    public static IServiceCollection AddZeroMqMultiTwoWay(
        this IServiceCollection services,
        EUrlConfiguration[] urlConfigs,
        params Type[]? handlerTypes)
    {
        // Handler DI 
        if (handlerTypes != null)
        {
            foreach (var type in handlerTypes)
            {
                services.AddSingleton(type);
                services.AddSingleton(typeof(BaseZeroMqTwoWayHandler), provider => provider.GetRequiredService(type));
            }
        }

        services.AddSingleton<IZeroMqTwoWayFactory, ZeroMqTwoWayFactory>();

        // 3. Ép hệ thống khởi tạo sẵn các kết nối ngay khi App Start (Né việc Lazy Loading)
        // Dùng IHostedService để chạy ngầm lúc app vừa lên
        services.AddHostedService(provider =>
        {
            var factory = provider.GetRequiredService<IZeroMqTwoWayFactory>();
            return new ZeroMqWarmupHostedService(factory, urlConfigs);
        });

        return services;
    }
}

public class ZeroMqWarmupHostedService : IHostedService
{
    private readonly IZeroMqTwoWayFactory _factory;
    private readonly EUrlConfiguration[] _urlConfigs;

    public ZeroMqWarmupHostedService(IZeroMqTwoWayFactory factory, EUrlConfiguration[] urlConfigs)
    {
        _factory = factory;
        _urlConfigs = urlConfigs;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_urlConfigs is not { Length: > 0 })
            return;

        // IPC
        foreach (var config in _urlConfigs)
        {
            try
            {
                await _factory.CreateClientAsync(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZeroMQ Warmup Error] {config} fail: {ex.Message}");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}