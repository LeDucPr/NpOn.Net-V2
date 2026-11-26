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
    public int HandlerCount { get; private set; }
    public IDbDriverFactory? Factory { get; set; }
    public EDb DbType { get; set; }

    private readonly List<BaseRedisBroadcastHandler> _handlers = [];
    private readonly RedisConnectOption _connectOption;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly List<(BaseRedisBroadcastHandler Handler, Action<RedisChannel, RedisValue> Callback)> _subscriptions = [];

    private CancellationTokenSource _cts = new();
    private Timer? _reconnectTimer;
    private bool _isDestroyed;

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

    public bool BuildFactory(out string? errorString)
    {
        errorString = null;

        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        _isDestroyed = false;
        _subscriptions.Clear();

        // Publisher-only nodes have HandlerCount == 0; allocate at least 1 pooled driver for PublishAsync.
        int poolSize = Math.Max(HandlerCount, 1);
        Factory = new RedisDriverFactory(_connectOption, poolSize);

        if (HandlerCount == 0)
        {
            return true;
        }

        foreach (BaseRedisBroadcastHandler handler in _handlers)
        {
            Action<RedisChannel, RedisValue> callback = (c, v) =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await handler.ParseAndTriggerAsync(c.ToString(), v).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Guard callback so faulty payloads never terminate ThreadPool threads.
                    }
                });
            };
            _subscriptions.Add((handler, callback));
        }

        var (allSubscribed, subscribeError) = TrySubscribeAllAsync().GetAwaiter().GetResult();
        errorString = subscribeError;
        if (!allSubscribed)
        {
            // Schedule background retry so a temporary Redis outage at startup auto-heals once Redis returns.
            ScheduleReconnectSubscribe();
        }

        return allSubscribed;
    }

    private async Task<(bool Success, string? Error)> TrySubscribeAllAsync()
    {
        if (Factory == null || _subscriptions.Count == 0 || _isDestroyed)
            return (true, null);

        var errors = new List<string>();
        foreach (var (handler, callback) in _subscriptions)
        {
            NpOnDbConnection? connection = null;
            try
            {
                connection = await Factory.GetConnectionAsync().ConfigureAwait(false);
                if (connection?.Driver == null || !connection.Driver.IsValidSession)
                {
                    errors.Add($"Failed to connect for channel {handler.Channel}.");
                    continue;
                }

                var command = new RedisDbCommand(RedisChannel.Literal(handler.Channel), callback);
                var result = await connection.Driver.Execute(command).ConfigureAwait(false);
                if (!result?.Status ?? true)
                {
                    errors.Add($"Failed to subscribe to channel {handler.Channel}.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Subscribe exception for channel {handler.Channel}: {ex.Message}");
            }
            finally
            {
                if (connection != null)
                {
                    Factory.ReleaseConnection(connection);
                }
            }
        }

        if (errors.Count > 0)
            return (false, string.Join("; ", errors));

        return (true, null);
    }

    private void ScheduleReconnectSubscribe()
    {
        if (_isDestroyed || _cts.IsCancellationRequested)
            return;

        _reconnectTimer ??= new Timer(async _ =>
        {
            if (_isDestroyed || _cts.IsCancellationRequested)
                return;

            if (!await _lifecycleLock.WaitAsync(0).ConfigureAwait(false))
                return;

            try
            {
                if (_isDestroyed || _cts.IsCancellationRequested)
                    return;

                var (ok, _) = await TrySubscribeAllAsync().ConfigureAwait(false);
                if (ok && _reconnectTimer != null)
                {
                    await _reconnectTimer.DisposeAsync().ConfigureAwait(false);
                    _reconnectTimer = null;
                }
            }
            catch
            {
                // Keep retrying until broker comes back online.
            }
            finally
            {
                _lifecycleLock.Release();
            }
        }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
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

        handler.AssignCancellationToken(factory._cts.Token);
        factory._handlers.Add(handler);
        factory.HandlerCount++;
        return factory;
    }

    public void DestroyInternal()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;

        if (!_cts.IsCancellationRequested)
            _cts.Cancel();

        _reconnectTimer?.Dispose();
        _reconnectTimer = null;

        if (Factory != null)
        {
            Factory.Reset().GetAwaiter().GetResult();
            Factory = null;
        }

        foreach (var handler in _handlers)
        {
            if (handler is IDisposable disposableHandler)
                disposableHandler.Dispose();
        }

        _handlers.Clear();
        _subscriptions.Clear();
        HandlerCount = 0;

        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public async Task<INpOnWrapperResult?> PublishAsync(BaseBroadcastMessage message)
    {
        if (_isDestroyed)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);

        var messageType = message.GetType();
        Type? underlyingType = null;
        if (messageType.IsGenericType && messageType.GetGenericTypeDefinition() == typeof(RedisBroadcastMessage<>))
        {
            underlyingType = messageType.GetGenericArguments().FirstOrDefault();
        }

        var handler = _handlers.FirstOrDefault(h =>
            h.MessageType == messageType || (underlyingType != null && h.MessageType == underlyingType));

        // Publisher-only nodes may not have a local subscriber handler for this channel.
        string? channel = !string.IsNullOrWhiteSpace(message.Channel)
            ? message.Channel
            : handler?.Channel;

        if (string.IsNullOrWhiteSpace(channel))
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.CommandNotSupported);

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
        Factory ??= new RedisDriverFactory(_connectOption, Math.Max(HandlerCount, 1));

        NpOnDbConnection? connection = null;
        try
        {
            connection = await Factory.GetConnectionAsync().ConfigureAwait(false);
            if (connection?.Driver is not RedisDriver redisDriver ||
                redisDriver.GetConnection() is not ConnectionMultiplexer multiplexer ||
                !multiplexer.IsConnected)
            {
                return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                    .SetFail(EDbError.Connection);
            }

            var subscriber = multiplexer.GetSubscriber();
            var receiverCount = await subscriber.PublishAsync(RedisChannel.Literal(channel), redisValue)
                .ConfigureAwait(false);
            return new RedisValueWrapper(new RedisValueContainer(receiverCount));
        }
        catch
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.Connection);
        }
        finally
        {
            if (connection != null && Factory != null)
            {
                Factory.ReleaseConnection(connection);
            }
        }
    }

    public void Dispose()
    {
        DestroyInternal();
        _cts.Dispose();
        _lifecycleLock.Dispose();
    }
}
