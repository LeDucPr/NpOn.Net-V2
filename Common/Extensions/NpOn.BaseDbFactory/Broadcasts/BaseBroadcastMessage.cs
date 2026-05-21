namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

// [ProtoContract]
public abstract class BaseBroadcastMessage
{
    public abstract required string Channel { get; set; }
    public virtual string? Message { get; set; }
}