namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

public abstract class BaseBroadcastHandler : IDisposable
{
    private readonly Func<BaseBroadcastMessage, Task<bool>>? _logic;

    protected CancellationToken CancellationToken { get; private set; } = CancellationToken.None;

    // reference Unsubscribe Dispose
    private readonly BaseBroadcastTrigger _trigger;
    private bool _isDisposed;

    private BaseBroadcastHandler(BaseBroadcastTrigger trigger)
    {
        _trigger = trigger;
        _trigger.OnMessageReceived += BaseHandler;
    }
    
    public void AssignCancellationToken(CancellationToken token) // dispose from DI
        => CancellationToken = token;

    protected BaseBroadcastHandler(
        BaseBroadcastTrigger trigger,
        Func<BaseBroadcastMessage, Task<bool>>? logic = null) : this(trigger)
    {
        _logic = logic;
    }

    protected virtual async Task<bool> BaseHandler(BaseBroadcastMessage message)
    {
        if (await Validator(message) && _logic != null)
            return await _logic.Invoke(message);
        return false;
    }

    protected abstract Task<bool> Validator(BaseBroadcastMessage message);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;
        if (disposing)
            _trigger.OnMessageReceived -= BaseHandler; // Gc auto
        _isDisposed = true;
    }
}