using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using NpOn.TrackerConstant.Broadcasts;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.Messages;

public sealed class WarningRedisBroadCastMessage
{
    public string? MessageLog { get; set; }
    public string? ServiceName { get; set; }
    public DateTime? WarningDateUtc { get; set; }
    public ETrackerLogLevel TrackerLogLevel { get; set; }
}