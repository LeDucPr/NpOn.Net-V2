using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.FactoryResults;
using Common.Infrastructures.NpOn.RedisExtCm.Commands;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using StackExchange.Redis;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory;

public class RedisFactoryWrapper : BaseDbFactoryWrapper, IRedisFactoryWrapper
{
    private static readonly TimeSpan OutageCooldown = TimeSpan.FromSeconds(2);
    private long _circuitOpenUntilUtcTicks;

    public RedisFactoryWrapper(
        string openConnectString, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Redis;
        Factory = new RedisDriverFactory(
            new RedisConnectOption()
                .SetConnectionString(openConnectString),
            connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public RedisFactoryWrapper(
        INpOnConnectOption connectOption, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Redis;
        if (connectOption is not RedisConnectOption)
            throw new ArgumentException("connectOption must be a RedisConnectOption");
        Factory = new RedisDriverFactory(connectOption, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    /// <summary>
    /// Fast-fail Redis executor: bypasses BaseDbFactoryWrapper's 30-second acquisition backoff
    /// so cache/broadcast calls degrade in milliseconds when Dragonfly/Redis is unreachable
    /// and automatically probe recovery once the short cooldown expires.
    /// </summary>
    private async Task<INpOnWrapperResult?> ExecuteRedisFastFailAsync(RedisDbCommand command)
    {
        if (DateTime.UtcNow.Ticks < Interlocked.Read(ref _circuitOpenUntilUtcTicks))
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }

        var factory = Factory;
        if (factory == null)
        {
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }

        NpOnDbConnection? connection = null;
        try
        {
            connection = await factory.GetConnectionAsync().ConfigureAwait(false);
            if (connection == null || !connection.Driver.IsValidSession)
            {
                OpenCircuit();
                return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
            }

            INpOnWrapperResult result = await connection.Driver.Execute(command).ConfigureAwait(false);
            if (!result.Status && !connection.Driver.IsValidSession)
            {
                OpenCircuit();
            }
            else
            {
                Interlocked.Exchange(ref _circuitOpenUntilUtcTicks, 0);
            }

            return result;
        }
        catch (Exception ex) when (ex is RedisConnectionException or TimeoutException or ObjectDisposedException)
        {
            OpenCircuit();
            return new RedisValueWrapper(new RedisValueContainer(RedisValue.Null)).SetFail(EDbError.Connection);
        }
        finally
        {
            if (connection != null)
            {
                factory.ReleaseConnection(connection);
            }
        }
    }

    private void OpenCircuit()
    {
        Interlocked.Exchange(ref _circuitOpenUntilUtcTicks, DateTime.UtcNow.Add(OutageCooldown).Ticks);
    }

    // public Redis

    #region Single Operations

    #region Generic Type

    public Task<INpOnWrapperResult?> GetAsBaseResult(string key)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Get);
        return ExecuteRedisFastFailAsync(command);
    }

    public Task<INpOnWrapperResult?> SetAsBaseResult(string key, string value, TimeSpan? expiry = null)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Set, value, expiry ?? TimeSpan.FromMinutes(5));
        return ExecuteRedisFastFailAsync(command);
    }

    public Task<INpOnWrapperResult?> DeleteAsBaseResult(string key)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Delete);
        return ExecuteRedisFastFailAsync(command);
    }

    #endregion Generic Type


    #region Redis Wrapper Type

    public async Task<RedisValueWrapper?> Get(string key)
    {
        var result = await GetAsBaseResult(key);
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> Set(string key, string value, TimeSpan? expiry = null)
    {
        var result = await SetAsBaseResult(key, value, expiry ?? TimeSpan.FromMinutes(5));
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> Delete(string key)
    {
        var result = await DeleteAsBaseResult(key);
        return result as RedisValueWrapper;
    }

    #endregion Redis Wrapper Type

    #endregion Single Operations


    #region Bulk Operations

    #region Generic Type

    public Task<INpOnWrapperResult?> GetManyAsBaseResult(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.GetMany, redisKeys);
        return ExecuteRedisFastFailAsync(command);
    }

    public Task<INpOnWrapperResult?> SetManyAsBaseResult(
        Dictionary<string, string> keyValues, TimeSpan? expiry = null)
    {
        var pairs = keyValues
            .Select(kvp => new KeyValuePair<RedisKey, RedisValue>(kvp.Key, kvp.Value))
            .ToArray();
        var command = new RedisDbCommand(pairs, expiry ?? TimeSpan.FromMinutes(5));
        return ExecuteRedisFastFailAsync(command);
    }

    public Task<INpOnWrapperResult?> DeleteManyAsBaseResult(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.DeleteMany, redisKeys);
        return ExecuteRedisFastFailAsync(command);
    }

    #endregion Generic Type

    #region Redis Wrapper Type

    public async Task<RedisValueWrapper?> GetMany(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.GetMany, redisKeys);
        var result = await ExecuteRedisFastFailAsync(command);
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> SetMany(Dictionary<string, string> keyValues,
        TimeSpan? expiry = null)
    {
        var pairs = keyValues
            .Select(kvp => new KeyValuePair<RedisKey, RedisValue>(kvp.Key, kvp.Value))
            .ToArray();
        var command = new RedisDbCommand(pairs, expiry ?? TimeSpan.FromMinutes(5));
        var result = await ExecuteRedisFastFailAsync(command);
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> DeleteMany(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.DeleteMany, redisKeys);
        var result = await ExecuteRedisFastFailAsync(command);
        return result as RedisValueWrapper;
    }

    #endregion Redis Wrapper Type

    #endregion Bulk Operations


    #region Customize

    public async Task<RedisValueWrapper?> CustomizeString(string commandText)
    {
        var result = await ExecuteRedisFastFailAsync(new RedisDbCommand(commandText));
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> CustomizeCommand(INpOnDbCommand command)
    {
        RedisDbCommand redisCommand = command as RedisDbCommand ?? new RedisDbCommand(command.CommandText);
        var result = await ExecuteRedisFastFailAsync(redisCommand);
        return result as RedisValueWrapper;
    }

    public Task<INpOnWrapperResult?> CustomizeStringAsBaseResult(string commandText)
    {
        return ExecuteRedisFastFailAsync(new RedisDbCommand(commandText));
    }

    public Task<INpOnWrapperResult?> CustomizeCommandAsBaseResult(INpOnDbCommand command)
    {
        RedisDbCommand redisCommand = command as RedisDbCommand ?? new RedisDbCommand(command.CommandText);
        return ExecuteRedisFastFailAsync(redisCommand);
    }

    public Task<INpOnWrapperResult?> Publish(string channel)
    {
        RedisDbCommand redisCommand = new RedisDbCommand(channel, message: null);
        return ExecuteRedisFastFailAsync(redisCommand);
    }

    public Task<INpOnWrapperResult?> Subscribe(BaseBroadcastMessage message)
    {
        RedisDbCommand redisCommand = new RedisDbCommand(message);
        return ExecuteRedisFastFailAsync(redisCommand);
    }

    #endregion Customize
}