using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Commands;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Results;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public class ZeroMqTwoWayFactoryWrapper : IZeroMqTwoWayFactoryWrapper
{
    public int HandlerCount { get; private set; }
    public IDbDriverFactory? Factory { get; set; }
    public EDb DbType { get; set; }

    private readonly List<BaseZeroMqTwoWayHandler> _handlers = [];
    private readonly ZeroMqConnectOption _connectOption;
    private bool _isDestroyed;

    public bool BuildFactory(out string? errorString)
    {
        errorString = null;

        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        _isDestroyed = false;

        // Nếu không có handler nào, ta vẫn có thể dùng Factory để send
        int connCount = HandlerCount == 0 ? 1 : HandlerCount;
        Factory = new ZeroMqDriverFactory(_connectOption, connCount);

        foreach (var handler in _handlers)
        {
            var channel = handler.Channel;
            Func<string, Task<string>> callback = async (payload) =>
            {
                return await handler.ParseAndTriggerAsync(payload);
            };

            var command = new ZeroMqCommand(channel, callback);
            
            var connection = Factory.GetConnectionAsync().GetAwaiter().GetResult();
            var result = connection?.Driver.Execute(command).GetAwaiter().GetResult();
            if (!result?.Status ?? false)
            {
                errorString = $"Failed to register handler for channel {channel}.";
                return false;
            }
        }

        return true;
    }

    public ZeroMqTwoWayFactoryWrapper(INpOnConnectOption connectOption)
    {
        DbType = EDb.ZeroMqRunAsDbFlow;
        connectOption = connectOption.SetSessionTimeout(0);
        if (connectOption is not ZeroMqConnectOption zmqConnectOption)
            throw new ArgumentException(
                $"Expected {nameof(ZeroMqConnectOption)} but received {connectOption.GetType().Name}.",
                nameof(connectOption));
        _connectOption = zmqConnectOption;
    }

    public static ZeroMqTwoWayFactoryWrapper? operator +(ZeroMqTwoWayFactoryWrapper? factory,
        BaseZeroMqTwoWayHandler? handler)
    {
        if (factory == null || handler == null)
            return factory;

        if (factory._isDestroyed)
            throw new ObjectDisposedException(nameof(ZeroMqTwoWayFactoryWrapper),
                "Cannot add handlers after DestroyInternal has been called.");

        factory._handlers.Add(handler);
        factory.HandlerCount++;
        return factory;
    }

    public void DestroyInternal()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        _handlers.Clear();
        HandlerCount = 0;
    }

    public async Task<INpOnWrapperResult?> SendAsync<TRequest>(string channel, TRequest request)
    {
        var json = JsonModeWithCache.ToJson(request);
        var command = new ZeroMqCommand(channel, json);
        
        var connection = Factory?.ValidConnections?.FirstOrDefault();
        if (connection == null)
            return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);

        if (connection is not NpOnDbConnection npOnDbConnection // parse type from connection
            || npOnDbConnection.Driver is not ZeroMqDriver zmqDriver)
            return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);

        return await zmqDriver.Execute(command);
    }

    public void Dispose()
    {
        DestroyInternal();
    }
}
