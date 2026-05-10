using Common.Extensions.NpOn.CommonBaseDomain;

namespace MicroServices.Tracker.Contract.NpOn.TrackerServiceDomain;

public abstract class BaseTrackerDomain : BaseDomain
{
    public BaseTrackerDomain()
    {
    }

    #region Field Config

    public override Dictionary<string, string>? FieldMap { get; protected set; }

    protected override void FieldMapper()
    {
        FieldMap ??= new();
        // FieldMap.Add(nameof(Id), "id");
        // FieldMap.Add(nameof(CreatedAt), "created_at");
    }

    #endregion Field Config
}