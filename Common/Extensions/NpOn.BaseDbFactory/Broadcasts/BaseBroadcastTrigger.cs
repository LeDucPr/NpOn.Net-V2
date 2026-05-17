namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

/// <summary>
/// EventAdd on broadcast
/// </summary>
public abstract class BaseBroadcastTrigger
{
    public event BroadcastEvent? OnMessageReceived;

    protected virtual async Task IncomingOnMessageReceived(BaseBroadcastMessage message)
    {
        if (OnMessageReceived != null)
        {
            await OnMessageReceived.Invoke(message);
        }
    }
}