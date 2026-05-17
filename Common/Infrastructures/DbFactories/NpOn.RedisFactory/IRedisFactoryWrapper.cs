using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.RedisExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory;

public interface IRedisFactoryWrapper : IDbFactoryWrapper
{
    #region Single Operations

    Task<INpOnWrapperResult?> GetAsBaseResult(string key);
    Task<INpOnWrapperResult?> SetAsBaseResult(string key, string value, TimeSpan? expiry = null);
    Task<INpOnWrapperResult?> DeleteAsBaseResult(string key);
    Task<RedisValueWrapper?> Get(string key);
    Task<RedisValueWrapper?> Set(string key, string value, TimeSpan? expiry = null);
    Task<RedisValueWrapper?> Delete(string key);

    #endregion Single Operations


    #region Bulk Operations

    Task<INpOnWrapperResult?> GetManyAsBaseResult(params string[] keys);
    Task<INpOnWrapperResult?> SetManyAsBaseResult(Dictionary<string, string> keyValues, TimeSpan? expiry = null);
    Task<INpOnWrapperResult?> DeleteManyAsBaseResult(params string[] keys);
    Task<RedisValueWrapper?> GetMany(params string[] keys);
    Task<RedisValueWrapper?> SetMany(Dictionary<string, string> keyValues, TimeSpan? expiry = null);
    Task<RedisValueWrapper?> DeleteMany(params string[] keys);

    #endregion Bulk Operations


    #region Customize

    Task<RedisValueWrapper?> CustomizeString(string commandText);
    Task<RedisValueWrapper?> CustomizeCommand(INpOnDbCommand command);
    Task<INpOnWrapperResult?> CustomizeStringAsBaseResult(string commandText);
    Task<INpOnWrapperResult?> CustomizeCommandAsBaseResult(INpOnDbCommand command);

    #endregion Customize
}