using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBroadcastHandler : BaseBroadcastHandler
{
    protected BaseRedisBroadcastHandler(BaseRedisBaseBroadcastTrigger trigger,
        Func<BaseBroadcastMessage, Task<bool>> logicFunc) : base(trigger, logicFunc)
    {
    }

    protected override Task<bool> Validator(BaseBroadcastMessage message) // Pattern Matching
    {
        return Task.FromResult(message is not RedisBroadcastMessage);
    }
}