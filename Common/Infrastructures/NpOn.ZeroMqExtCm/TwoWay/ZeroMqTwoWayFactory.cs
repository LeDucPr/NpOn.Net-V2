using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public class ZeroMqTwoWayFactory : IZeroMqTwoWayFactory
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly IWrapperCacheStore<EUrlConfiguration, IZeroMqTwoWayProvider> CacheStore =
        new WrapperCacheStore<EUrlConfiguration, IZeroMqTwoWayProvider>();

    public ZeroMqTwoWayFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IZeroMqTwoWayProvider? TryGet(EUrlConfiguration urlConfiguration)
    {
        return CacheStore.TryGetValue(urlConfiguration, out var value) ? value : null;
    }

    public async Task<bool> TrySendTo<TRequest>(IEnumerable<EUrlConfiguration>? configurations, TRequest request)
    {
        var configurationList = configurations?.ToList();
        if (configurationList is not { Count: > 0 })
            return true;
        var sendTasks = configurationList.Select(async configuration =>
        {
            IZeroMqTwoWayProvider? provider = TryGet(configuration);
            if (provider == null)
                return false; 
            var result = await provider.SendAsync(request);
            return result?.Status ?? false;
        });
        bool[] results = await Task.WhenAll(sendTasks);
        return results.All(status => status); // true when all same 
    }

    public async Task<IZeroMqTwoWayProvider> CreateClientAsync(EUrlConfiguration urlConfig)
    {
        // Sử dụng GetOrAddAsync chống Cache Stampede khi đăng ký URL liên tục
        return await CacheStore.GetOrAddAsync(urlConfig, async (config) =>
        {
            string rawUrl = config.GetAppSettingConfig().AsEmptyString();
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? uri))
                throw new ArgumentException($"URL cấu hình {config} sai định dạng.");

            // IPC use with default port
            string ipcConnectionString = $"ipc://npon-zmq-pipe-{uri.Port}";

            var connectOption = new ZeroMqConnectOption();
            connectOption.SetConnectionString(ipcConnectionString);

            var factoryWrapper = new ZeroMqTwoWayProvider(connectOption);

            // Handler - DI (if exist)
            var handlers = _serviceProvider.GetServices<BaseZeroMqTwoWayHandler>().ToArray();
            foreach (var handler in handlers)
            {
                factoryWrapper += handler;
            }

            // Build Factory triggerUrl
            string? errorString = null;
            if (!(factoryWrapper?.BuildFactory(out errorString) ?? false))
            {
                throw new InvalidOperationException($"Build ZeroMQ Factory thất bại: {errorString}");
            }

            return factoryWrapper;
        });
    }
}