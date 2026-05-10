using Common.Extensions.NpOn.CommonMode;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using ProtoBuf;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceReadModel.ReadModels;

[ProtoContract]
public class SystemLogRModel : BaseTrackerRModelFromGrpcTable
{
    [ProtoMember(1)] public required Guid Id { get; set; }
    [ProtoMember(2)] public DateTime EventDate { get; set; }
    [ProtoMember(3)] public ETrackerLogLevel Level { get; set; }
    [ProtoMember(4)] public string? Source { get; set; }
    [ProtoMember(5)] public string? Message { get; set; }
    [ProtoMember(6)] public ETrackerLogType TrackerLogType { get; set; }
    public ETrackerLogType[] TrackerLogTypes => TrackerLogType.GetFlags();
    [ProtoMember(7)] public new DateTime CreatedAt { get; set; } 

    protected override void FieldMapper()
    {
        FieldMap ??= [];
        FieldMap.Add(nameof(Id), "id");
        FieldMap.Add(nameof(EventDate), "event_date");
        FieldMap.Add(nameof(Level), "level");
        FieldMap.Add(nameof(Source), "source");
        FieldMap.Add(nameof(Message), "message");
        FieldMap.Add(nameof(TrackerLogType), "log_type");
    }
}