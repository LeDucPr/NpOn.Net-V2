namespace MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages;

public class TrackerTest2WayRequestCommand
{
    public string MessageId { get; set; } = Guid.NewGuid().ToString();
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class TrackerTest2WayResponseCommand
{
    public string Message { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
