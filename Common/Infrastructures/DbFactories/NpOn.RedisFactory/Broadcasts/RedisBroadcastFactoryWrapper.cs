using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.FactoryResults;
using Common.Infrastructures.NpOn.RedisExtCm.Commands;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using StackExchange.Redis;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public class RedisBroadcastFactoryWrapper : IRedisBroadcastFactoryWrapper
{
    public int HandlerCount { get; private set; } /*= 0;*/
    public IDbDriverFactory? Factory { get; set; }
    public EDb DbType { get; set; }

    private readonly List<BaseRedisBroadcastHandler> _handlers = [];
    private readonly RedisConnectOption _connectOption;

    // Master switch to immediately stop all background Handlers
    // Non-readonly: must be re-creatable after DestroyInternal
    private CancellationTokenSource _cts = new();
    private bool _isDestroyed;

    public bool BuildFactory(out string? errorString)
    {
        errorString = null;

        // Properly destroy the old Factory before creating a new one
        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        _isDestroyed = false;

        if (HandlerCount == 0)
        {
            errorString = "No handlers registered. Use operator+ to add handlers before calling BuildFactory.";
            return false;
        }

        // Create Factory
        Factory = new RedisDriverFactory(_connectOption, HandlerCount);
        
        foreach (BaseRedisBroadcastHandler handler in _handlers)
        {
            var channel = handler.Channel;
            Action<RedisChannel, RedisValue> callback = (c, v) =>
            {
                _ = handler.ParseAndTriggerAsync(c.ToString(), v);
            };

            var command = new RedisDbCommand(RedisChannel.Literal(channel), callback);
            var connection = Factory.GetConnectionAsync().GetAwaiter().GetResult();
            // BuildFactory is synchronous, so we block on the async task
            var result = connection?.Driver.Execute(command).GetAwaiter().GetResult();
            if (!result?.Status ?? false)
            {
                errorString = $"Failed to subscribe to channel {channel}.";
                return false;
            }
        }

        return true;
    }

    public RedisBroadcastFactoryWrapper(INpOnConnectOption connectOption)
    {
        DbType = EDb.Redis;
        connectOption = connectOption.SetSessionTimeout(0);
        if (connectOption is not RedisConnectOption redisConnectOption)
            throw new ArgumentException(
                $"Expected {nameof(RedisConnectOption)} but received {connectOption.GetType().Name}.",
                nameof(connectOption));
        _connectOption = redisConnectOption;
    }

    // Factory + Handler
    public static RedisBroadcastFactoryWrapper? operator +(RedisBroadcastFactoryWrapper? factory,
        BaseRedisBroadcastHandler? handler)
    {
        if (factory == null || handler == null)
            return factory;

        if (factory._isDestroyed)
            throw new ObjectDisposedException(nameof(RedisBroadcastFactoryWrapper),
                "Cannot add handlers after DestroyInternal has been called.");

        // Assign cancellation token to handler to synchronize the stop signal
        handler.AssignCancellationToken(factory._cts.Token);
        factory._handlers.Add(handler);
        factory.HandlerCount++;
        return factory;
    }

    public void DestroyInternal()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        // Signal all handlers to stop via CancellationToken
        if (!_cts.IsCancellationRequested)
            _cts.Cancel();

        // Close all connections then release
        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        // Dispose all handlers (unsubscribes from trigger events)
        foreach (var handler in _handlers)
        {
            if (handler is IDisposable disposableHandler)
                disposableHandler.Dispose();
        }

        _handlers.Clear();
        HandlerCount = 0;

        _cts.Dispose(); // dispose the old CTS and prepare a fresh one for potential reuse
        _cts = new CancellationTokenSource();
    }

    public async Task<INpOnWrapperResult?> PublishAsync(BaseBroadcastMessage message)
    {
        var messageType = message.GetType();
        Type? underlyingType = null;
        if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(RedisBroadcastMessage<>))
        {
            underlyingType = messageType.GetGenericArguments().FirstOrDefault();
        }

        var handler = _handlers.FirstOrDefault(h => h.MessageType == messageType || (underlyingType != null && h.MessageType == underlyingType));
        if (handler == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.CommandNotSupported);

        var channel = handler.Channel;
        if (string.IsNullOrWhiteSpace(message.Channel))
        {
            message.Channel = channel;
        }

        string? json;
        if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(RedisBroadcastMessage<>))
            json = message.Message;
        else
            json = JsonModeWithCache.ToJson(message);

        var redisValue = (RedisValue)json;

        var connection = Factory?.ValidConnections?.FirstOrDefault();
        if (connection == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.Connection);

        if (connection is not NpOnDbConnection npOnDbConnection
            || npOnDbConnection.Driver is not RedisDriver redisDriver
            || redisDriver.GetConnection() is not ConnectionMultiplexer multiplexer)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.Connection);

        var subscriber = multiplexer.GetSubscriber();
        var receiverCount = await subscriber.PublishAsync(RedisChannel.Literal(channel), redisValue);
        return new RedisValueWrapper(new RedisValueContainer(receiverCount));
    }

    public void Dispose()
    {
        DestroyInternal();
        _cts.Dispose();
    }
}