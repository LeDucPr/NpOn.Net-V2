using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;

[ProtoContract]
public class BulkSyncQueriesCommand : BaseGeneralCommand
{
    [ProtoMember(1)] public List<TblMasterSyncItemCommand> Queries { get; set; } = [];
    [ProtoMember(2)] public string? DeployVersion { get; set; }
    [ProtoMember(3)] public bool IsFullReload { get; set; }
}

[ProtoContract]
public class TblMasterSyncItemCommand
{
    [ProtoMember(1)] public string? Id { get; set; }
    [ProtoMember(2)] public required string Code { get; set; }
    [ProtoMember(3)] public string? Description { get; set; }
    [ProtoMember(4)] public string? ExecFunc { get; set; }
    [ProtoMember(5)] public string? Query { get; set; }
    [ProtoMember(6)] public int ExecType { get; set; }
    [ProtoMember(7)] public string? ServiceName { get; set; }
    [ProtoMember(8)] public int? DbType { get; set; }
    [ProtoMember(9)] public List<FldQueryMasterSyncCommand> Fields { get; set; } = [];
}

[ProtoContract]
public class FldQueryMasterSyncCommand
{
    [ProtoMember(1)] public string? Id { get; set; }
    [ProtoMember(2)] public required string FieldName { get; set; }
    [ProtoMember(3)] public int? FieldType { get; set; }
    [ProtoMember(4)] public string? FieldTypeString { get; set; }
    [ProtoMember(5)] public int OrderSort { get; set; }
    [ProtoMember(6)] public string? Description { get; set; }
}
