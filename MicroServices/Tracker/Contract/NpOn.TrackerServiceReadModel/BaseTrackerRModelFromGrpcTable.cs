using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceReadModel.ReadModels;
using ProtoBuf;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceReadModel;

[ProtoContract]
[ProtoInclude(100, typeof(SystemLogRModel))]
public abstract class BaseTrackerRModelFromGrpcTable : NpOnBaseGrpcObject
{
    [ProtoMember(1)] public DateTime? CreatedAt { get; set; }
    [ProtoMember(2)] public DateTime? UpdatedAt { get; set; }
    [ProtoMember(3)] public Guid? ProcessUId { get; set; }
    [ProtoMember(4)] public int? TotalRow { get; set; }

    #region Field Config
    public override Dictionary<string, string>? FieldMap { get; protected set; }

    protected override void BaseFieldMapper()
    {
        base.BaseFieldMapper();
        FieldMap!.Add(nameof(TotalRow), "total_row");
        FieldMap.Add(nameof(CreatedAt), "created_at");
        FieldMap.Add(nameof(UpdatedAt), "updated_at");
        FieldMap.Add(nameof(ProcessUId), "process_uid");
    }

    #endregion Field Config

    protected abstract override void FieldMapper();
}