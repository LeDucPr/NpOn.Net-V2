using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
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

    // Pub/Sub properties
    public RedisChannel Channel { get; } // struct RedisChannelOption = 0 default 
    public Action<RedisChannel, RedisValue>? SubscribeHandler { get; }

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


    // Constructor for Publish
    public RedisDbCommand(RedisChannel channel, string? message = null) : base(EDb.Redis,
        $"{ERedisCommand.Publish} {channel}")
    {
        CommandTypeTypeType = ERedisCommand.Publish;
        Channel = channel;
    }

    // Constructor for Subscribe
    public RedisDbCommand(RedisChannel channel, Action<RedisChannel, RedisValue> handler) : base(EDb.Redis,
        $"{ERedisCommand.Subscribe} {channel}")
    {
        CommandTypeTypeType = ERedisCommand.Subscribe;
        Channel = channel;
        SubscribeHandler = handler;
    }

    public RedisDbCommand(BaseBroadcastMessage message) : base(EDb.Redis,
        $"{ERedisCommand.Subscribe} {message.Channel}")
    {
        CommandTypeTypeType = ERedisCommand.Subscribe;
        Channel = RedisChannel.Literal(message.Channel);
        Value = message.Message;
    }

    // Constructor for Unsubscribe
    public RedisDbCommand(RedisChannel channel) : base(EDb.Redis,
        $"{ERedisCommand.Unsubscribe} {channel}")
    {
        CommandTypeTypeType = ERedisCommand.Unsubscribe;
        Channel = channel;
    }
}