using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBaseBroadcastTrigger<T> : BaseBroadcastTrigger
{
    public abstract string Channel { get; }

    public Task TriggerAsync(string channel, string message)
    {
        var msg = new RedisBroadcastMessage<T> { Channel = channel, Message = message };
        return IncomingOnMessageReceived(msg);
    }
} 