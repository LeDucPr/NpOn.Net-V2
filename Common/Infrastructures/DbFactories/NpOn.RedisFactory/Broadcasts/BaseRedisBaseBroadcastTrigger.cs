using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBaseBroadcastTrigger<T> : BaseBroadcastTrigger
{
    public Task TriggerAsync(RedisBroadcastMessage<T> msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Channel))
            msg.Channel = Channel;
        else if (msg.Channel != Channel)
            throw new Exception("Channel not match");
        return IncomingOnMessageReceived(msg);
    }
}