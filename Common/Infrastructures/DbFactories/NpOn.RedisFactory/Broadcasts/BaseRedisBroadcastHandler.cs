using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using StackExchange.Redis;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBroadcastHandler : BaseBroadcastHandler
{
    public abstract string Channel { get; }
    public abstract Type MessageType { get; }
    public abstract Task ParseAndTriggerAsync(string channel, RedisValue value);

    protected BaseRedisBroadcastHandler(BaseBroadcastTrigger trigger, Func<BaseBroadcastMessage, Task<bool>> logicFunc)
        : base(trigger, logicFunc)
    {
    }

    protected override Task<bool> Validator(BaseBroadcastMessage? message) // Pattern Matching
    {
        if (message == null)
            return Task.FromResult(false);
        var messageType = message.GetType();
        bool isBroadcast = messageType.IsGenericType &&
                           messageType.GetGenericTypeDefinition() == typeof(RedisBroadcastMessage<>);

        return Task.FromResult(isBroadcast);
    }
}

public abstract class BaseRedisBroadcastHandler<T> : BaseRedisBroadcastHandler
{
    public BaseRedisBaseBroadcastTrigger<T> Trigger { get; }

    public override string Channel => Trigger.Channel;
    public override Type MessageType => typeof(T);

    protected BaseRedisBroadcastHandler(BaseRedisBaseBroadcastTrigger<T> trigger,
        Func<BaseBroadcastMessage, Task<bool>> logicFunc) : base(trigger, logicFunc)
    {
        Trigger = trigger;
    }

    public override Task ParseAndTriggerAsync(string channel, RedisValue value)
    {
        var msg = value.ToRedisBroadcastMessage<T>(channel);
        return Trigger.TriggerAsync(msg);
    }
}