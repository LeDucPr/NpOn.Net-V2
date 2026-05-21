using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class BaseRedisBaseBroadcastTrigger<T> : BaseBroadcastTrigger
{
    public abstract string Channel { get; }

    public Task TriggerAsync(string message)
    {
        var msg = new RedisBroadcastMessage<T> { Channel = Channel, Message = message };
        return IncomingOnMessageReceived(msg);
    }

    public Task TriggerAsync(RedisBroadcastMessage msg)
    {
        if (string.IsNullOrWhiteSpace(msg.Channel))
            msg.Channel = Channel;
        else if (msg.Channel != Channel)
            throw new Exception("Channel not match");
        return IncomingOnMessageReceived(msg);
    }
}