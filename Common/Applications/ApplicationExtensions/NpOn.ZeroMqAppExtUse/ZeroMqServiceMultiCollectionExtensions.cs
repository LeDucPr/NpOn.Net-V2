using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;


namespace Common.Applications.ApplicationsExtensions.NpOn.ZeroMqAppExtUse;

public static class ZeroMqServiceMultiCollectionExtensions
{
    /// <summary>
    /// Multi ZeroMQ IPC
    /// </summary>
    public static IServiceCollection AddZeroMqMultiClients(
        this IServiceCollection services, 
        EUrlConfiguration[] targetUrls)
    {
        // Đăng ký con Factory quản lý Cache gửi tin làm Singleton
        services.AddSingleton<IZeroMqTwoWayFactory, ZeroMqTwoWayFactory>();

        // Ép khởi tạo sớm (Warmup) các kết nối Client (Connect) tới các URL mục tiêu ngay khi App lên
        services.AddHostedService(provider => 
        {
            var factory = provider.GetRequiredService<IZeroMqTwoWayFactory>();
            return new ZeroMqWarmupHostedService(factory, targetUrls);
        });

        return services;
    }

    /// <summary>
    /// Nhận tin xử lý thì đăng ký thêm (Server/Bind) Handler
    /// </summary>
    public static IServiceCollection AddZeroMqReceiverHandlers(
        this IServiceCollection services, 
        params Type[]? handlerTypes)
    {
        if (handlerTypes == null) return services;

        foreach (var type in handlerTypes)
        {
            services.AddSingleton(type);
            services.AddSingleton(typeof(BaseZeroMqTwoWayHandler), provider => provider.GetRequiredService(type));
        }

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