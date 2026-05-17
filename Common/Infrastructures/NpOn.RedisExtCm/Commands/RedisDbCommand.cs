using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using StackExchange.Redis;

namespace Common.Infrastructures.NpOn.RedisExtCm.Commands;

public class RedisDbCommand : NpOnDbCommand
{
    private readonly EDb _dbType = EDb.Redis;
    public ERedisCommand CommandTypeTypeType { get; }
    public string Key { get; }
    public RedisValue Value { get; }
    public RedisKey[]? Keys { get; }
    public KeyValuePair<RedisKey, RedisValue>[]? KeyValues { get; }
    public TimeSpan? Expiry { get; }
    public When WhenCondition { get; private set; } = When.Always;
    public CommandFlags[] CommandFlagsUse { get; private set; } = [CommandFlags.None];

    // to custom 
    public RedisDbCommand(string commandText, TimeSpan? expiry = null)
        : base(EDb.Redis, "commandText")
    {
        CommandTypeTypeType = ERedisCommand.Customize;
    }

    public RedisDbCommand(string key, ERedisCommand commandType, RedisValue value = default, TimeSpan? expiry = null) :
        base(EDb.Redis, $"{commandType} {key}")
    {
        CommandTypeTypeType = commandType;
        Key = key;
        Value = value;
        Expiry = expiry;
    }

    // get/delete many
    public RedisDbCommand(ERedisCommand commandType, RedisKey[] keys) : base(EDb.Redis,
        $"{commandType} {keys.Select(x => x.AsDefaultString()).AsArrayJoin()}")
    {
        CommandTypeTypeType = commandType;
        Keys = keys;
    }

    // Constructor for SetMany
    public RedisDbCommand(KeyValuePair<RedisKey, RedisValue>[] keyValues, TimeSpan? expiry = null) : base(EDb.Redis,
        $"{ERedisCommand.SetMany} {keyValues.Select(x => x.Key.AsDefaultString()).AsArrayJoin()}")
    {
        CommandTypeTypeType = ERedisCommand.SetMany;
        Expiry = expiry;
        KeyValues = keyValues;
    }
}