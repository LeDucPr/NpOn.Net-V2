using System.Collections.Concurrent;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.RedisExtCm.Commands;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using StackExchange.Redis;

namespace Common.Infrastructures.NpOn.RedisExtCm.Connections;

public class RedisDriver : NpOnDbDriver
{
    private static readonly ConcurrentDictionary<string, long> ConnectCooldownUntilUtcTicks = new(StringComparer.Ordinal);
    private static readonly TimeSpan ConnectCooldownDuration = TimeSpan.FromSeconds(2);
    private const int DefaultFastConnectTimeoutMs = 500;
    private const int DefaultFastOperationTimeoutMs = 1000;

    private ConnectionMultiplexer? _connection;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    public override string Name { get; set; } = "Redis";
    public override string Version { get; set; } = "Unknown";

    public override bool IsValidSession => _connection is { IsConnected: true };

    public override object? GetConnection() => _connection;

    public RedisDriver(INpOnConnectOption option) : base(option)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return;
        }

        string? connectionString = Option.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        if (ConnectCooldownUntilUtcTicks.TryGetValue(connectionString, out long cooldownUntilTicks)
            && DateTime.UtcNow.Ticks < cooldownUntilTicks)
        {
            throw new RedisConnectionException(
                ConnectionFailureType.UnableToConnect,
                "Redis endpoint is in outage cooldown.");
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsValidSession)
            {
                return;
            }

            if (ConnectCooldownUntilUtcTicks.TryGetValue(connectionString, out cooldownUntilTicks)
                && DateTime.UtcNow.Ticks < cooldownUntilTicks)
            {
                throw new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    "Redis endpoint is in outage cooldown.");
            }

            // Replace stale / broken multiplexer when disconnected so reconnect recovers cleanly.
            if (_connection != null)
            {
                try
                {
                    await _connection.CloseAsync(allowCommandsToComplete: false).ConfigureAwait(false);
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore errors disposing dead multiplexer.
                }
                _connection = null;
            }

            var configOptions = ConfigurationOptions.Parse(connectionString);
            configOptions.ReconnectRetryPolicy ??= new ExponentialRetry(500, 5000);
            if (!connectionString.Contains("connectTimeout", StringComparison.OrdinalIgnoreCase))
                configOptions.ConnectTimeout = DefaultFastConnectTimeoutMs;
            if (!connectionString.Contains("syncTimeout", StringComparison.OrdinalIgnoreCase))
                configOptions.SyncTimeout = DefaultFastOperationTimeoutMs;
            if (!connectionString.Contains("asyncTimeout", StringComparison.OrdinalIgnoreCase))
                configOptions.AsyncTimeout = DefaultFastOperationTimeoutMs;
            if (!connectionString.Contains("connectRetry", StringComparison.OrdinalIgnoreCase))
                configOptions.ConnectRetry = 1;

            try
            {
                _connection = await ConnectionMultiplexer.ConnectAsync(configOptions).ConfigureAwait(false);
            }
            catch
            {
                ConnectCooldownUntilUtcTicks[connectionString] =
                    DateTime.UtcNow.Add(ConnectCooldownDuration).Ticks;
                throw;
            }

            if (_connection is { IsConnected: true })
            {
                ConnectCooldownUntilUtcTicks.TryRemove(connectionString, out _);
                var endpoints = _connection.GetEndPoints();
                if (endpoints.Length > 0)
                {
                    var server = _connection.GetServer(endpoints[0]);
                    Version = server.Version.ToString();
                    Name = $"Redis on {server.EndPoint}";
                }
            }
            else
            {
                ConnectCooldownUntilUtcTicks[connectionString] =
                    DateTime.UtcNow.Add(ConnectCooldownDuration).Ticks;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public override async Task DisconnectAsync()
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection != null)
            {
                try
                {
                    await _connection.CloseAsync().ConfigureAwait(false);
                    await _connection.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Ignore close errors during shutdown.
                }
                _connection = null;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _connection == null)
        {
            try
            {
                await ConnectAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
            }

            if (!IsValidSession || _connection == null)
            {
                return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
            }
        }

        if (command is not RedisDbCommand redisCommand)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.CommandNotSupported);
        }

        try
        {
            IDatabase db = Option.DatabaseIndex == null
                ? _connection.GetDatabase()
                : _connection.GetDatabase(Option.DatabaseIndex.AsDefaultInt());

            return redisCommand.CommandTypeTypeType switch
            {
                ERedisCommand.Set => new RedisValueWrapper(new RedisValueContainer(
                    await db.StringSetAsync(redisCommand.Key, redisCommand.Value, redisCommand.Expiry))),
                ERedisCommand.Get => new RedisValueWrapper(new RedisValueContainer(
                    await db.StringGetAsync(redisCommand.Key))),
                ERedisCommand.Delete => new RedisValueWrapper(new RedisValueContainer(
                    await db.KeyDeleteAsync(redisCommand.Key))),
                ERedisCommand.GetMany when redisCommand.Keys != null => new RedisValueWrapper(new RedisValueContainer(
                    await db.StringGetAsync(redisCommand.Keys))),
                ERedisCommand.SetMany when redisCommand.KeyValues != null => await HandleSetMany(db, redisCommand),
                ERedisCommand.DeleteMany when redisCommand.Keys != null => new RedisValueWrapper(
                    new RedisValueContainer(await db.KeyDeleteAsync(redisCommand.Keys))),

                // Pub/Sub
                ERedisCommand.Publish => await HandlePublish(redisCommand),
                ERedisCommand.Subscribe => await HandleSubscribe(redisCommand),
                ERedisCommand.Unsubscribe => await HandleUnsubscribe(redisCommand),

                _ => new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                    .SetFail(EDbError.CommandNotSupported)
            };
        }
        catch (ObjectDisposedException)
        {
            _connection = null;
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }
        catch (RedisConnectionException)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }
        catch (Exception)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.RedisExecute);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    private static Task<RedisValueWrapper> HandleSetMany(IDatabase db, RedisDbCommand redisCommand)
    {
        var batch = db.CreateBatch();
        foreach (var pair in redisCommand.KeyValues!)
        {
            _ = batch.StringSetAsync(pair.Key, pair.Value, redisCommand.Expiry);
        }

        batch.Execute();
        return Task.FromResult(new RedisValueWrapper(new RedisValueContainer(true)));
    }

    private async Task<INpOnWrapperResult> HandlePublish(RedisDbCommand redisCommand)
    {
        if (_connection == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);

        var subscriber = _connection.GetSubscriber();
        var receiverCount = await subscriber.PublishAsync(redisCommand.Channel, redisCommand.Value);
        return new RedisValueWrapper(new RedisValueContainer(receiverCount));
    }

    private async Task<INpOnWrapperResult> HandleSubscribe(RedisDbCommand redisCommand)
    {
        if (_connection == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);

        if (redisCommand.SubscribeHandler == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.CommandNotSupported);

        var subscriber = _connection.GetSubscriber();
        await subscriber.SubscribeAsync(redisCommand.Channel, redisCommand.SubscribeHandler);
        return new RedisValueWrapper(new RedisValueContainer(true));
    }

    private async Task<INpOnWrapperResult> HandleUnsubscribe(RedisDbCommand redisCommand)
    {
        if (_connection == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);

        var subscriber = _connection.GetSubscriber();
        await subscriber.UnsubscribeAsync(redisCommand.Channel);
        return new RedisValueWrapper(new RedisValueContainer(true));
    }
}
