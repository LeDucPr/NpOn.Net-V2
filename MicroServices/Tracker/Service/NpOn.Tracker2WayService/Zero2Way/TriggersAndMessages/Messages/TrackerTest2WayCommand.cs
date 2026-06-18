using Common.Extensions.NpOn.CommonMode;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Triggers;

namespace MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Messages;

public class TrackerTest2WayRequestCommand : TrackerLog2WayTrigger
{
    public TrackerTest2WayRequestCommand() : base()
    {
    }
    public string MessageId { get; set; } = IndexerMode.CreateGuidAsString();
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class TrackerTest2WayResponseCommand : TrackerLog2WayTrigger
{
    public TrackerTest2WayResponseCommand() : base()
    {
    }
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}