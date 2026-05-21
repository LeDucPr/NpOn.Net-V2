using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;


public class RedisBroadcastMessage<T> : BaseBroadcastMessage
{
    private T? _value;
    private bool _isParsed;

    public T? Value
    {
        get
        {
            if (!_isParsed)
            {
                _value = JsonModeWithCache.FromJson<T>(Message);
                _isParsed = true;
            }
            return _value;
        }
        set
        {
            base.Message = JsonModeWithCache.ToJsonAsNull(value);
            _value = value;
            _isParsed = true;
        }
    }

    public override required string Channel { get; set; }

    public override string? Message
    {
        get => base.Message;
        set
        {
            base.Message = value;
            ResetCache(); 
        }
    }

    private void ResetCache()
    {
        _value = default;
        _isParsed = false;
    }
}

public static class RedisBroadcastMessageExtensions
{
    public static RedisBroadcastMessage<T> ToRedisBroadcastMessage<T>(this StackExchange.Redis.RedisValue value, string channel)
    {
        return new RedisBroadcastMessage<T>
        {
            Channel = channel,
            Message = value.HasValue ? value.ToString() : null
        };
    }
}