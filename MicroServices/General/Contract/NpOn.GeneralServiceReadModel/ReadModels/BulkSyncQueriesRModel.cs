using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;

[ProtoContract]
public record BulkSyncQueriesRModel : BaseGeneralRModel
{
    [ProtoMember(1)] public int SyncedCount { get; set; }
    [ProtoMember(2)] public string DeployVersion { get; set; } = string.Empty;
    [ProtoMember(3)] public int BroadcastReceivers { get; set; }
}
