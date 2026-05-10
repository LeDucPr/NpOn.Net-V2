using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;
using ProtoBuf;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand;

[ProtoContract]
[ProtoInclude(100, typeof(TrackerLogAddCommand))]

public abstract class BaseTrackerCommand : CommonAbsQuery
{
    [ProtoMember(1)] public override bool Status { get; set; }
    [ProtoMember(2)] public override EErrorCode? ErrorCode { get; set; }
    [ProtoMember(3)] public override string? Object { get; set; }
    [ProtoMember(4)] public sealed override DateTime QueryUtcTime { get; init; } = DateTime.UtcNow;
    [ProtoMember(5)] public virtual Guid? ProcessUId { get; set; }
    // [ProtoMember(6)] public string? LoginUId { get; set; }
}