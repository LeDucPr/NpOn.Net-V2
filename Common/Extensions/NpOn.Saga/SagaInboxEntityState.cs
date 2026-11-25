using Common.Extensions.NpOn.CommonEnums;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// State tracking for Transactional Inbox Causality Guard (10-byte compound key).
/// Protects against Lost Updates, out-of-order events, and broken audit trails.
/// </summary>
public class SagaInboxEntityState
{
    public ESagaEntityType EntityType { get; set; }
    public long KeyHash { get; set; }
    public long LastAppliedVersion { get; set; }
    public string LastMessageId { get; set; } = string.Empty;
    public string CompositeKey { get; set; } = string.Empty;
    public bool IsQuarantined { get; set; }
    public int RetryCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Evaluates version transition taking into account retry counts and entity quarantine state.
    /// </summary>
    public ESagaInboxProcessingResult EvaluateTransitionWithRetry(
        long preVersion,
        long postVersion,
        int currentRetryCount,
        int maxRetries = 3)
    {
        if (IsQuarantined)
        {
            return ESagaInboxProcessingResult.DeadLetter;
        }

        if (currentRetryCount >= maxRetries)
        {
            IsQuarantined = true;
            return ESagaInboxProcessingResult.DeadLetter;
        }

        return EvaluateTransition(preVersion, postVersion);
    }

    /// <summary>
    /// Evaluates version transition for an incoming event.
    /// </summary>
    public ESagaInboxProcessingResult EvaluateTransition(long preVersion, long postVersion)
    {
        // Genesis: first time creation
        if (LastAppliedVersion == 0 && preVersion == 0 && postVersion >= 1)
        {
            return ESagaInboxProcessingResult.Applied;
        }

        // Stale or duplicate message that arrived late
        if (postVersion <= LastAppliedVersion)
        {
            return ESagaInboxProcessingResult.DuplicateDropped;
        }

        // Strict consecutive causality: pre_version matches current state
        if (preVersion == LastAppliedVersion)
        {
            return ESagaInboxProcessingResult.Applied;
        }

        // Gap in versions (e.g. current is 3, event claims pre_version is 5) -> hold in deferred buffer
        if (preVersion > LastAppliedVersion)
        {
            return ESagaInboxProcessingResult.DeferredOutOfOrder;
        }

        return ESagaInboxProcessingResult.ConflictRejected;
    }
}
