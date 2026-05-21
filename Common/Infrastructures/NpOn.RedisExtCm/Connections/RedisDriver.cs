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
    private ConnectionMultiplexer? _connection;
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

        if (Option.ConnectionString != null)
            _connection ??= await ConnectionMultiplexer.ConnectAsync(Option.ConnectionString); // 

        if (_connection is { IsConnected: true })
        {
            var server = _connection.GetServer(_connection.GetEndPoints().First());
            Version = server.Version.ToString();
            Name = $"Redis on {server.EndPoint}";
        }
    }

    public override async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _connection == null)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }

        if (command is not RedisDbCommand redisCommand)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null))
                .SetFail(EDbError.CommandNotSupported);
        }

        try
        {
            IDatabase db;
            if (Option.DatabaseIndex == null)
                db = _connection.GetDatabase();
            else
                db = _connection.GetDatabase(Option.DatabaseIndex.AsDefaultInt());
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
        catch (Exception)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.RedisExecute);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    private static async Task<RedisValueWrapper> HandleSetMany(IDatabase db, RedisDbCommand redisCommand)
    {
        // ManySET command itself doesn't support expiry. We use a transaction (batch) to achieve this.
        var batch = db.CreateBatch();
        foreach (var pair in redisCommand.KeyValues!)
        {
            _ = batch.StringSetAsync(pair.Key, pair.Value, redisCommand.Expiry);
        }

        batch.Execute();
        // Since we're using a batch, the result isn't directly available. We'll return success.
        // The actual set operations are "fire and forget" within the batch.
        return new RedisValueWrapper(new RedisValueContainer(true));
    }

    private async Task<INpOnWrapperResult> HandlePublish(RedisDbCommand redisCommand)
    {
        if (_connection == null)
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);

        var subscriber = _connection.GetSubscriber();
        var receiverCount = await subscriber.PublishAsync(redisCommand.Channel, redisCommand.Value);
        return new RedisValueWrapper(new RedisValueContainer( /*(RedisValue)*/receiverCount));
    }

    // for subscribe (always open connection)
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