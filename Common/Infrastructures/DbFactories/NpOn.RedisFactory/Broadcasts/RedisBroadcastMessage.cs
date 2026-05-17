using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;

public abstract class RedisBroadcastMessage : BaseBroadcastMessage
{
    // Sửa chính tả Chanel -> Channel
    public string? Channel { get; set; } 
    public virtual string? Message { get; set; }
}

public class RedisBroadcastMessage<T> : RedisBroadcastMessage
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