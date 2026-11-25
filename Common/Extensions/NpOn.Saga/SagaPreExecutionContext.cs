using System.Buffers;
using Common.Extensions.NpOn.CommonEnums;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Pre-execution context holding Phase 1 extracted ADN payload in RAM.
/// Eliminates network round-trip serialization overhead.
/// </summary>
public class SagaPreExecutionContext<T> : IDisposable
{
    private T[]? _rentedBuffer;
    private readonly int _count;

    public ReadOnlySpan<T> Items => _rentedBuffer != null ? _rentedBuffer.AsSpan(0, _count) : ReadOnlySpan<T>.Empty;
    public int Count => _count;
    public ESagaEntityType EntityType { get; }
    public string TableName { get; }

    public SagaPreExecutionContext(ESagaEntityType entityType, string tableName, IEnumerable<T> source)
    {
        EntityType = entityType;
        TableName = tableName;

        var list = source as IReadOnlyList<T> ?? source.ToList();
        _count = list.Count;
        _rentedBuffer = ArrayPool<T>.Shared.Rent(_count);

        for (int i = 0; i < _count; i++)
        {
            _rentedBuffer[i] = list[i];
        }
    }

    public void Dispose()
    {
        if (_rentedBuffer != null)
        {
            ArrayPool<T>.Shared.Return(_rentedBuffer, clearArray: true);
            _rentedBuffer = null;
        }
    }
}

/// <summary>
/// Phase 2 ADN Fusion Engine combining DB classification (is_insert / action) with RAM ADN payload.
/// Produces minimal Delta-Only audit records with zero heap reallocation.
/// </summary>
public static class SagaMergeFusionEngine
{
    /// <summary>
    /// Fuses single record outcome from DB returning tokens into a SagaEntityAudit.
    /// </summary>
    public static SagaEntityAudit FuseSingle(
        ESagaEntityType entityType,
        string compositeKey,
        bool isInsert,
        long currentVersion,
        string? deltaJson,
        string actorId = "system")
    {
        long keyHash = SagaKeyHasher.Hash64(compositeKey.AsSpan());

        EMergeClassification operation;
        long fromVer;
        long toVer;

        if (isInsert)
        {
            operation = EMergeClassification.Inserted;
            fromVer = 0;
            toVer = currentVersion >= 1 ? currentVersion : 1;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(deltaJson) || deltaJson == "{}")
            {
                operation = EMergeClassification.NoOp;
            }
            else
            {
                operation = EMergeClassification.Updated;
            }
            fromVer = Math.Max(0, currentVersion - 1);
            toVer = currentVersion;
        }

        return new SagaEntityAudit
        {
            EntityType = entityType,
            KeyHash = keyHash,
            CompositeKey = compositeKey,
            OperationType = operation,
            FromVersion = fromVer,
            ToVersion = toVer,
            ActorId = actorId,
            ChangedFieldsJson = deltaJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}

