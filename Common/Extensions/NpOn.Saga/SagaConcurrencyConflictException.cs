namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Thrown when an Optimistic Concurrency Control (OCC) conflict is detected
/// (e.g. database returned affected_rows = 0 due to version mismatch).
/// </summary>
public class SagaConcurrencyConflictException : Exception
{
    public ESagaEntityType EntityType { get; }
    public string EntityKey { get; }
    public long ExpectedVersion { get; }

    public SagaConcurrencyConflictException(ESagaEntityType entityType, string entityKey, long expectedVersion)
        : base($"OCC Conflict detected for {entityType} with key '{entityKey}'. Expected version was {expectedVersion}.")
    {
        EntityType = entityType;
        EntityKey = entityKey;
        ExpectedVersion = expectedVersion;
    }

    public SagaConcurrencyConflictException(string message) : base(message)
    {
        EntityType = ESagaEntityType.Unknown;
        EntityKey = string.Empty;
        ExpectedVersion = 0;
    }

    public SagaConcurrencyConflictException(string message, Exception innerException) : base(message, innerException)
    {
        EntityType = ESagaEntityType.Unknown;
        EntityKey = string.Empty;
        ExpectedVersion = 0;
    }
}

