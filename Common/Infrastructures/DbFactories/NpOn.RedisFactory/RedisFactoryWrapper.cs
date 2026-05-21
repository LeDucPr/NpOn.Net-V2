using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.BaseDbFactory.Generics;
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

    // public Redis

    #region Single Operations

    #region Generic Type

    public Task<INpOnWrapperResult?> GetAsBaseResult(string key)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Get);
        return ExecuteAsync(command);
    }

    public Task<INpOnWrapperResult?> SetAsBaseResult(string key, string value, TimeSpan? expiry = null)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Set, value, expiry ?? TimeSpan.FromMinutes(5));
        return ExecuteAsync(command);
    }

    public Task<INpOnWrapperResult?> DeleteAsBaseResult(string key)
    {
        var command = new RedisDbCommand(key, ERedisCommand.Delete);
        return ExecuteAsync(command);
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

    public async Task<INpOnWrapperResult?> GetManyAsBaseResult(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.GetMany, redisKeys);
        var result = await ExecuteAsync(command);
        return result;
    }

    public async Task<INpOnWrapperResult?> SetManyAsBaseResult(
        Dictionary<string, string> keyValues, TimeSpan? expiry = null)
    {
        var pairs = keyValues
            .Select(kvp => new KeyValuePair<RedisKey, RedisValue>(kvp.Key, kvp.Value))
            .ToArray();
        var command = new RedisDbCommand(pairs, expiry ?? TimeSpan.FromMinutes(5));
        var result = await ExecuteAsync(command);
        return result;
    }

    public async Task<INpOnWrapperResult?> DeleteManyAsBaseResult(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.DeleteMany, redisKeys);
        var result = await ExecuteAsync(command);
        return result;
    }

    #endregion Generic Type

    #region Redis Wrapper Type

    public async Task<RedisValueWrapper?> GetMany(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.GetMany, redisKeys);
        var result = await ExecuteAsync(command);
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> SetMany(Dictionary<string, string> keyValues,
        TimeSpan? expiry = null)
    {
        var pairs = keyValues
            .Select(kvp => new KeyValuePair<RedisKey, RedisValue>(kvp.Key, kvp.Value))
            .ToArray();
        var command = new RedisDbCommand(pairs, expiry ?? TimeSpan.FromMinutes(5));
        var result = await ExecuteAsync(command);
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> DeleteMany(params string[] keys)
    {
        var redisKeys = keys.Select(k => (RedisKey)k).ToArray();
        var command = new RedisDbCommand(ERedisCommand.DeleteMany, redisKeys);
        var result = await ExecuteAsync(command);
        return result as RedisValueWrapper;
    }

    #endregion Redis Wrapper Type

    #endregion Bulk Operations


    #region Customize

    public async Task<RedisValueWrapper?> CustomizeString(string commandText)
    {
        var result = await ExecuteAsync(new RedisDbCommand(commandText));
        return result as RedisValueWrapper;
    }

    public async Task<RedisValueWrapper?> CustomizeCommand(INpOnDbCommand command)
    {
        RedisDbCommand redisCommand = command as RedisDbCommand ?? new RedisDbCommand(command.CommandText);
        var result = await ExecuteAsync(redisCommand);
        return result as RedisValueWrapper;
    }

    public async Task<INpOnWrapperResult?> CustomizeStringAsBaseResult(string commandText)
    {
        var result = await ExecuteAsync(new RedisDbCommand(commandText));
        return result;
    }

    public async Task<INpOnWrapperResult?> CustomizeCommandAsBaseResult(INpOnDbCommand command)
    {
        RedisDbCommand redisCommand = command as RedisDbCommand ?? new RedisDbCommand(command.CommandText);
        var result = await ExecuteAsync(redisCommand);
        return result;
    }

    public async Task<INpOnWrapperResult?> Publish(string channel)
    {
        RedisDbCommand redisCommand = new RedisDbCommand(channel, message: null);
        var result = await ExecuteAsync(redisCommand);
        return result;
    }

    public async Task<INpOnWrapperResult?> Subscribe(BaseBroadcastMessage message)
    {
        RedisDbCommand redisCommand = new RedisDbCommand(message);
        var result = await ExecuteAsync(redisCommand);
        return result;
    }

    #endregion Customize
}