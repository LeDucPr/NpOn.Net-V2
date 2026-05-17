namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

public abstract class BaseBroadcastHandler
{
    private readonly Func<BaseBroadcastMessage, Task<bool>>? _logic;

    private BaseBroadcastHandler(BaseBroadcastTrigger trigger)
    {
        trigger.OnMessageReceived += BaseHandler;
    }

    protected BaseBroadcastHandler(
        BaseBroadcastTrigger trigger,
        Func<BaseBroadcastMessage, Task<bool>>? logic = null) : this(trigger)
    {
        _logic = logic;
    }

    protected virtual async Task<bool> BaseHandler(BaseBroadcastMessage message) // Pattern Matching
    {
        if (await Validator(message) && _logic != null)
            return await _logic?.Invoke(message)!;
        return false;
    }

    protected abstract Task<bool> Validator(BaseBroadcastMessage message);
}