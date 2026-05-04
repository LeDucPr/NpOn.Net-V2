using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceCommand;

[ProtoContract]
[ProtoInclude(100, typeof(TblFldExecutionCommand))]
[ProtoInclude(200, typeof(DomainActionCommand))]
public abstract class BaseGeneralCommand : CommonAbsQuery
{
    [ProtoMember(1)] public override bool Status { get; set; }
    [ProtoMember(2)] public override EErrorCode? ErrorCode { get; set; }
    [ProtoMember(3)] public override string? Object { get; set; }
    [ProtoMember(4)] public sealed override DateTime QueryUtcTime { get; init; } = DateTime.UtcNow;
}