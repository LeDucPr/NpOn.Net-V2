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
            try
            {
                // Thay vì TryGet (trả về null nếu background warmup task chưa hoàn thành),
                // CreateClientAsync sẽ await task đang chạy trong cache hoặc tạo mới nếu chưa có.
                IZeroMqTwoWayProvider provider = await CreateClientAsync(configuration);
                var result = await provider.SendAsync(request);
                return result?.Status ?? false;
            }
            catch (Exception)
            {
                return false; 
            }
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

            string ipcConnectionString = ZeroMqIpcHelper.CombineConnectionStringIpc(uri.Port);

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