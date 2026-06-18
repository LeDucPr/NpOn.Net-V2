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

public class ZeroMqTwoWayProvider : IZeroMqTwoWayProvider
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
        if (HandlerCount > 0)
        {
            _connectOption.IsServerMode = true;
        }
        Factory = new ZeroMqDriverFactory(_connectOption, connCount);

        foreach (var handler in _handlers)
        {
            var channel = handler.Channel;
            Func<string, Task<string>> callback = async (payload) => await handler.ParseAndTriggerAsync(payload);

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

    public ZeroMqTwoWayProvider(INpOnConnectOption connectOption)
    {
        DbType = EDb.ZeroMqRunAsDbFlow;
        connectOption = connectOption.SetSessionTimeout(0);
        if (connectOption is not ZeroMqConnectOption zmqConnectOption)
            throw new ArgumentException(
                $"Expected {nameof(ZeroMqConnectOption)} but received {connectOption.GetType().Name}.",
                nameof(connectOption));
        _connectOption = zmqConnectOption;
    }

    public static ZeroMqTwoWayProvider? operator +(ZeroMqTwoWayProvider? factory,
        BaseZeroMqTwoWayHandler? handler)
    {
        if (factory == null || handler == null)
            return factory;

        if (factory._isDestroyed)
            throw new ObjectDisposedException(nameof(ZeroMqTwoWayProvider),
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

    public async Task<INpOnWrapperResult?> SendAsync<TRequest>(TRequest request)
    {
        var json = JsonModeWithCache.ToJson(request);
        var command = new ZeroMqCommand(/*channel*/string.Empty, json);
        if (Factory == null)
            return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);

        NpOnDbConnection? connection = null;
        try
        {
            connection = await Factory.GetConnectionAsync();
            if (connection == null)
                return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);

            if (connection.Driver is not ZeroMqDriver zmqDriver)
                return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);

            // chuyển sang dùng tín hiệu phân vùng cho đa kết nối trục tiếp thong qua ipc, cái này cần cấu hình lại đẻ có thể chuyển tiếp qua các địa chỉ kết nối khác
            return await zmqDriver.Execute(command);
        }
        catch (Exception)
        {
            return new ZeroMqResultSetWrapper().SetFail(EDbError.Connection);
        }
        finally
        {
            if (connection != null)
            {
                Factory.ReleaseConnection(connection);
            }
        }
    }

    public void Dispose()
    {
        DestroyInternal();
    }
}
