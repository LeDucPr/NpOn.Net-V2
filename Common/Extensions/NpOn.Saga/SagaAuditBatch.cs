using Common.Extensions.NpOn.CommonMode;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Contiguous batch of audit records for bulk binary ingestion.
/// </summary>
public class SagaAuditBatch
{
    public Guid BatchId { get; } = IndexerMode.CreateAuditId();
    public List<SagaEntityAudit> Audits { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public SagaAuditBatch(List<SagaEntityAudit> audits)
    {
        Audits = audits;
    }

    public SagaAuditBatch(IEnumerable<SagaEntityAudit> audits)
    {
        Audits = new List<SagaEntityAudit>(audits);
    }
}
