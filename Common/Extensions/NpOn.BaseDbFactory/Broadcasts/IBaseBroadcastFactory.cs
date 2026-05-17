namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

/// <summary>
/// Broadcast -> Handler + Connection(Hold) + EventAdd
/// </summary>
public interface IBaseBroadcastFactory
{
    // static abstract IBaseBroadcastFactory operator +(IBaseBroadcastFactory? factory, BaseBroadcastHandler? handler);
}