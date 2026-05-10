using System.Runtime.Serialization;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand;

[DataContract]
public class TrackerLogCommand
{
    [DataMember(Order = 1)]
    public DateTime Timestamp { get; set; }

    [DataMember(Order = 2)]
    public string Level { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public string Source { get; set; } = string.Empty;

    [DataMember(Order = 4)]
    public string Message { get; set; } = string.Empty;

    [DataMember(Order = 5)]
    public Dictionary<string, string> Attributes { get; set; } = new();
}
