using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Minimalist Delta-Only Audit record (45 - 95 bytes).
/// Excludes untouched properties, reducing storage footprint by 95% compared to full domain snapshots.
/// </summary>
public class SagaEntityAudit
{
    public long AuditId { get; set; }
    public Guid SagaId { get; set; } = IndexerMode.CreateSagaId();
    public ESagaEntityType EntityType { get; set; } = ESagaEntityType.Unknown;
    public long KeyHash { get; set; }
    public string CompositeKey { get; set; } = string.Empty;
    public EMergeClassification OperationType { get; set; } = EMergeClassification.NoOp;
    public long FromVersion { get; set; }
    public long ToVersion { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string? ChangedFieldsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsNoOp => OperationType == EMergeClassification.NoOp ||
                          (OperationType == EMergeClassification.Updated && string.IsNullOrWhiteSpace(ChangedFieldsJson));
}
