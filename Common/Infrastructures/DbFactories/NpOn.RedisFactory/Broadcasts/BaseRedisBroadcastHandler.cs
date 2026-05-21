using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBroadcastHandler : BaseBroadcastHandler
{
    public abstract string Channel { get; }
    public abstract Task TriggerAsync(string channel, string message);
    
    public abstract Task TriggerAsync(RedisBroadcastMessage redisBroadcastMessage);

    protected BaseRedisBroadcastHandler(BaseBroadcastTrigger trigger, Func<BaseBroadcastMessage, Task<bool>> logicFunc)
        : base(trigger, logicFunc)
    {
    }
}

public abstract class BaseRedisBroadcastHandler<T> : BaseRedisBroadcastHandler
{
    public BaseRedisBaseBroadcastTrigger<T> Trigger { get; }

    public override string Channel => Trigger.Channel;

    protected BaseRedisBroadcastHandler(BaseRedisBaseBroadcastTrigger<T> trigger,
        Func<BaseBroadcastMessage, Task<bool>> logicFunc) : base(trigger, logicFunc)
    {
        Trigger = trigger;
    }

    public override Task TriggerAsync(string channel, string message)
    {
        return Trigger.TriggerAsync(message);
    }

    public override Task TriggerAsync(RedisBroadcastMessage redisBroadcastMessage)
    {
        return Trigger.TriggerAsync(redisBroadcastMessage);
    }


    protected override Task<bool> Validator(BaseBroadcastMessage message) // Pattern Matching
    {
        // Only handle messages that match the expected type
        return Task.FromResult(message is RedisBroadcastMessage<T>);
    }
}