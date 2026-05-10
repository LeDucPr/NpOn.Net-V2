using System.Runtime.Serialization;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using ProtoBuf;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;

[DataContract]
public class TrackerLogAddCommand : BaseTrackerCommand
{
    [ProtoMember(1)]
    [DataMember(Order = 1)]
    public DateTime? EventDate { get; set; }

    [ProtoMember(2)]
    [DataMember(Order = 2)]
    public required ETrackerLogLevel Level { get; set; }

    [ProtoMember(3)]
    [DataMember(Order = 3)]
    public required string Source { get; set; }

    [ProtoMember(4)]
    [DataMember(Order = 4)]
    public required string Message { get; set; }

    [ProtoMember(5)]
    [DataMember(Order = 5)]
    public required ETrackerLogType[] TrackerLogTypes { get; set; }

    [ProtoMember(6)]
    [DataMember(Order = 6)]
    public Dictionary<string, string> Attributes { get; set; } = new();
}