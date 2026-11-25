using System.Threading.Channels;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.Saga;
using Xunit;

namespace NpOn.Core.Tests;

/// <summary>
/// Exhaustive failure scenarios and error resilience tests for Saga Engine:
/// - Channel / Queue: Backpressure, overflow, channel closed, cancellation timeouts, starvation, double-dispose.
/// - Inbox Causality Guard: Out-of-order gap detection, duplicate drop, stale messages, genesis conflicts, retry exhaustion, DLQ & quarantine isolation.
/// - Outbox & OCC: Concurrency conflict exceptions, buffer lifecycle and double-dispose safety.
/// - Key Hasher: Boundary null/empty, extreme 10,000-char keys, Unicode/accents/emojis.
/// - Merge Fusion: Empty/whitespace JSON NoOp handling, version boundary normalizations.
/// </summary>
public class SagaFailureScenariosTests
{
    #region 1. Queue & Channel Failures (SagaAuditChannel)

    [Fact]
    public void Channel_BoundedCapacity_WhenBufferFull_TryWriteShouldReturnFalseAsBackpressure()
    {
        // Arrange: Capacity of 2
        using var channel = new SagaAuditChannel(capacity: 2, BoundedChannelFullMode.Wait);
        var batch1 = new SagaAuditBatch(new List<SagaEntityAudit>());
        var batch2 = new SagaAuditBatch(new List<SagaEntityAudit>());
        var batch3 = new SagaAuditBatch(new List<SagaEntityAudit>());

        // Act & Assert
        Assert.True(channel.TryWrite(batch1), "1st write must succeed");
        Assert.True(channel.TryWrite(batch2), "2nd write must succeed");
        Assert.Equal(2, channel.Count);

        // 3rd write must be rejected by TryWrite (non-blocking backpressure indicator)
        bool write3 = channel.TryWrite(batch3);
        Assert.False(write3, "3rd write must return false when bounded buffer is saturated");
        Assert.Equal(2, channel.Count);

        // Dequeue one item to relieve backpressure
        Assert.True(channel.Reader.TryRead(out var readItem));
        Assert.NotNull(readItem);
        Assert.Equal(1, channel.Count);

        // Now 3rd item can be written successfully
        Assert.True(channel.TryWrite(batch3), "Write must succeed once buffer has space freed");
        Assert.Equal(2, channel.Count);
    }

    [Fact]
    public async Task Channel_Completed_WhenClosed_TryWriteShouldReturnFalse_AndWriteAsyncShouldThrowChannelClosedException()
    {
        // Arrange
        using var channel = new SagaAuditChannel(capacity: 5);
        var batch = new SagaAuditBatch(new List<SagaEntityAudit>());

        // Act: Complete channel
        channel.Complete();

        // Assert 1: TryWrite returns false on closed channel
        bool writeResult = channel.TryWrite(batch);
        Assert.False(writeResult, "TryWrite on completed/closed channel must return false");

        // Assert 2: WriteAsync throws ChannelClosedException
        await Assert.ThrowsAsync<ChannelClosedException>(async () =>
        {
            await channel.WriteAsync(batch);
        });
    }

    [Fact]
    public async Task Channel_WriteAsync_WhenBufferFullAndCancelled_ShouldThrowOperationCanceledException()
    {
        // Arrange: Buffer size of 1, filled immediately
        using var channel = new SagaAuditChannel(capacity: 1);
        var batch1 = new SagaAuditBatch(new List<SagaEntityAudit>());
        var batch2 = new SagaAuditBatch(new List<SagaEntityAudit>());

        Assert.True(channel.TryWrite(batch1));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert: WriteAsync on full buffer with cancellation token must abort with OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await channel.WriteAsync(batch2, cts.Token);
        });
    }

    [Fact]
    public void Channel_EmptyReader_TryRead_ShouldReturnFalseCleanlyWithoutBlocking()
    {
        // Arrange: Empty channel
        using var channel = new SagaAuditChannel(capacity: 10);

        // Act
        Assert.Equal(0, channel.Count);
        bool readSuccess = channel.Reader.TryRead(out var batch);

        // Assert
        Assert.False(readSuccess);
        Assert.Null(batch);
    }

    [Fact]
    public void Channel_Disposal_DoubleDispose_ShouldBeSafeAndIdempotent()
    {
        // Arrange
        var channel = new SagaAuditChannel(capacity: 10);

        // Act & Assert: First and second dispose must complete cleanly without exception
        var ex1 = Record.Exception(() => channel.Dispose());
        var ex2 = Record.Exception(() => channel.Dispose());

        Assert.Null(ex1);
        Assert.Null(ex2);
    }

    #endregion

    #region 2. Inbox Causality Guard & Poison Message Failures (SagaInboxEntityState)

    [Fact]
    public void Inbox_OutOfOrderEvent_PreVersionGreaterThanLastApplied_ShouldReturnDeferredOutOfOrder()
    {
        // Arrange: Entity currently at Version 3
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Device,
            KeyHash = 9999L,
            LastAppliedVersion = 3
        };

        // Act: Event arrives with preVersion = 5 (missing versions 4)
        var result = state.EvaluateTransition(preVersion: 5, postVersion: 6);

        // Assert: Held in deferred out-of-order buffer, state untouched
        Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder, result);
        Assert.Equal(3, state.LastAppliedVersion);
    }

    [Fact]
    public void Inbox_StaleAndLateMessages_PostVersionLessThanOrEqualToLastApplied_ShouldDropAsDuplicate()
    {
        // Arrange: Entity currently at Version 10
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Account,
            KeyHash = 1001L,
            LastAppliedVersion = 10
        };

        // Case 1: Extreme stale message (V1 -> V2)
        var staleResult = state.EvaluateTransition(preVersion: 1, postVersion: 2);
        Assert.Equal(ESagaInboxProcessingResult.DuplicateDropped, staleResult);

        // Case 2: Exact version replay (V9 -> V10)
        var replayResult = state.EvaluateTransition(preVersion: 9, postVersion: 10);
        Assert.Equal(ESagaInboxProcessingResult.DuplicateDropped, replayResult);

        // Entity state remains at version 10
        Assert.Equal(10, state.LastAppliedVersion);
    }

    [Fact]
    public void Inbox_GenesisConflict_WhenEntityAlreadyExists_GenesisEventShouldBeDroppedAsDuplicate()
    {
        // Arrange: Entity is already at Version 7
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Patient,
            KeyHash = 888L,
            LastAppliedVersion = 7
        };

        // Act: Late Genesis event arrives (V0 -> V1)
        var result = state.EvaluateTransition(preVersion: 0, postVersion: 1);

        // Assert: Must be dropped as duplicate / stale, never reset state
        Assert.Equal(ESagaInboxProcessingResult.DuplicateDropped, result);
        Assert.Equal(7, state.LastAppliedVersion);
    }

    [Fact]
    public void Inbox_ConflictRejected_WhenInconsistentVersionRewrite_ShouldReturnConflictRejected()
    {
        // Arrange: Entity at version 5
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Transaction,
            KeyHash = 555L,
            LastAppliedVersion = 5
        };

        // Act: Event with preVersion = 3 (< 5) but postVersion = 8 (> 5)
        // Indicates an illegal branch / conflicting causality attempt
        var result = state.EvaluateTransition(preVersion: 3, postVersion: 8);

        // Assert: Must be rejected as Conflict
        Assert.Equal(ESagaInboxProcessingResult.ConflictRejected, result);
        Assert.Equal(5, state.LastAppliedVersion);
    }

    [Fact]
    public void Inbox_RetryExhaustion_WhenReachingMaxRetries_ShouldTransitionToDeadLetterAndQuarantine()
    {
        // Arrange: Entity at version 1, max retries = 3
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Account,
            KeyHash = 12345L,
            LastAppliedVersion = 1,
            IsQuarantined = false
        };

        // Retry 1: Under limit, evaluates transition normally (gap detected -> DeferredOutOfOrder)
        var r1 = state.EvaluateTransitionWithRetry(preVersion: 3, postVersion: 4, currentRetryCount: 1, maxRetries: 3);
        Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder, r1);
        Assert.False(state.IsQuarantined);

        // Retry 2: Under limit
        var r2 = state.EvaluateTransitionWithRetry(preVersion: 3, postVersion: 4, currentRetryCount: 2, maxRetries: 3);
        Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder, r2);
        Assert.False(state.IsQuarantined);

        // Retry 3: Reaches maxRetries -> Triggers DLQ DeadLetter and entity Quarantine!
        var r3 = state.EvaluateTransitionWithRetry(preVersion: 3, postVersion: 4, currentRetryCount: 3, maxRetries: 3);
        Assert.Equal(ESagaInboxProcessingResult.DeadLetter, r3);
        Assert.True(state.IsQuarantined, "Entity must be marked as quarantined upon retry exhaustion");
    }

    [Fact]
    public void Inbox_QuarantinedEntity_SubsequentValidMessages_ShouldBeQuarantinedImmediately()
    {
        // Arrange: Entity is already quarantined
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Account,
            KeyHash = 12345L,
            LastAppliedVersion = 1,
            IsQuarantined = true
        };

        // Act: Even a perfectly valid consecutive event (pre=1, post=2) with retry=0 must be quarantined
        var result = state.EvaluateTransitionWithRetry(preVersion: 1, postVersion: 2, currentRetryCount: 0, maxRetries: 3);

        // Assert: Quarantined entities are fenced from normal pipeline
        Assert.Equal(ESagaInboxProcessingResult.DeadLetter, result);
    }

    #endregion

    #region 3. Outbox & OCC Failures

    [Fact]
    public void SagaConcurrencyConflictException_ShouldPreserveExactMetadataAndFormatting()
    {
        // Arrange & Act
        var ex = new SagaConcurrencyConflictException(ESagaEntityType.Device, "DEV_SER_9021", expectedVersion: 8);

        // Assert
        Assert.Equal(ESagaEntityType.Device, ex.EntityType);
        Assert.Equal("DEV_SER_9021", ex.EntityKey);
        Assert.Equal(8, ex.ExpectedVersion);
        Assert.Contains("DEV_SER_9021", ex.Message);
        Assert.Contains("Expected version was 8", ex.Message);

        // Test alternate constructors
        var innerEx = new InvalidOperationException("DB socket lost");
        var exWithInner = new SagaConcurrencyConflictException("Custom conflict msg", innerEx);
        Assert.Equal("Custom conflict msg", exWithInner.Message);
        Assert.Same(innerEx, exWithInner.InnerException);
        Assert.Equal(ESagaEntityType.Unknown, exWithInner.EntityType);
    }

    [Fact]
    public void PreExecutionContext_LifecycleAndBufferSafety_DoubleDisposeShouldBeSafe()
    {
        // Arrange: Populate items
        var testData = new List<string> { "item1", "item2", "item3", "item4", "item5" };
        var context = new SagaPreExecutionContext<string>(ESagaEntityType.Device, "devices", testData);

        // Act 1: Verify pre-dispose state
        Assert.Equal(5, context.Count);
        Assert.Equal(5, context.Items.Length);
        Assert.Equal("item1", context.Items[0]);
        Assert.Equal("item5", context.Items[4]);

        // Act 2: First Dispose returns buffer to ArrayPool
        context.Dispose();
        Assert.True(context.Items.IsEmpty, "Items span must be empty after disposal");

        // Act 3: Double dispose must NOT throw ObjectDisposedException or corrupt pool
        var ex = Record.Exception(() => context.Dispose());
        Assert.Null(ex);
    }

    #endregion

    #region 4. SagaKeyHasher Boundary & Extreme Tests

    [Fact]
    public void KeyHasher_BoundaryConditions_EmptyAndNullSpans_ShouldReturnZero()
    {
        // Act
        long charHash = SagaKeyHasher.Hash64(ReadOnlySpan<char>.Empty);
        long byteHash = SagaKeyHasher.Hash64(ReadOnlySpan<byte>.Empty);

        // Assert
        Assert.Equal(0L, charHash);
        Assert.Equal(0L, byteHash);
    }

    [Fact]
    public void KeyHasher_ExtremePayload_10000Characters_ShouldComputeDeterministicHashWithoutStackOverflow()
    {
        // Arrange: A massive 10,000-character key (exceeds stackalloc 256-byte limit, tests heap fallback)
        var longKey = new string('A', 10000);

        // Act: Compute 3 times
        long hash1 = SagaKeyHasher.Hash64(longKey.AsSpan());
        long hash2 = SagaKeyHasher.Hash64(longKey.AsSpan());
        long hash3 = SagaKeyHasher.Hash64(longKey.AsSpan());

        // Assert
        Assert.NotEqual(0L, hash1);
        Assert.Equal(hash1, hash2);
        Assert.Equal(hash2, hash3);
    }

    [Fact]
    public void KeyHasher_UnicodeAndAccents_VietnameseCharacters_ShouldBeDeterministicAndDivergent()
    {
        // Arrange: Realistic medical IoT Vietnamese composite keys
        const string key1 = "BệnhNhân_NguyễnVănA#KhoaTimMạch🏥#Phòng101_Giường02";
        const string key2 = "BệnhNhân_NguyễnVănB#KhoaTimMạch🏥#Phòng101_Giường02";
        const string key3 = "BệnhNhân_NguyễnVănA#KhoaCấpCứu🏥#Phòng101_Giường02";

        // Act
        long hash1A = SagaKeyHasher.Hash64(key1.AsSpan());
        long hash1B = SagaKeyHasher.Hash64(key1.AsSpan());
        long hash2 = SagaKeyHasher.Hash64(key2.AsSpan());
        long hash3 = SagaKeyHasher.Hash64(key3.AsSpan());

        // Assert: Determinism
        Assert.NotEqual(0L, hash1A);
        Assert.Equal(hash1A, hash1B);

        // Assert: Divergence (no collisions across different patient names or departments)
        Assert.NotEqual(hash1A, hash2);
        Assert.NotEqual(hash1A, hash3);
        Assert.NotEqual(hash2, hash3);
    }

    [Fact]
    public void KeyHasher_AvalancheEffect_SingleCharacterDifference_ShouldDivergeDrastically()
    {
        // Arrange: 1 bit/char difference
        const string baseKey = "ORDER_TX_20261119_00000001";
        const string modifiedKey = "ORDER_TX_20261119_00000002";

        // Act
        long hashA = SagaKeyHasher.Hash64(baseKey.AsSpan());
        long hashB = SagaKeyHasher.Hash64(modifiedKey.AsSpan());

        // Assert: Far apart hashes (no trivial correlation)
        Assert.NotEqual(hashA, hashB);
        Assert.True(Math.Abs(hashA - hashB) > 1000000L);
    }

    #endregion

    #region 5. SagaMergeFusionEngine Edge Cases & Empty JSON Handling

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    public void FusionEngine_DeltaUpdate_EmptyOrWhitespaceJson_ShouldBeTreatedAsNoOp(string? deltaJson)
    {
        // Arrange & Act
        var audit = SagaMergeFusionEngine.FuseSingle(
            ESagaEntityType.Account,
            compositeKey: "ACC_500",
            isInsert: false,
            currentVersion: 4,
            deltaJson: deltaJson,
            actorId: "system");

        // Assert: Classifies as NoOp because no actual field delta occurred
        Assert.Equal(EMergeClassification.NoOp, audit.OperationType);
        Assert.True(audit.IsNoOp);
        Assert.Equal(3, audit.FromVersion);
        Assert.Equal(4, audit.ToVersion);
    }

    [Fact]
    public void FusionEngine_InsertNormalization_ZeroVersion_ShouldNormalizeToOne()
    {
        // Arrange & Act: Insert with currentVersion = 0
        var audit = SagaMergeFusionEngine.FuseSingle(
            ESagaEntityType.Transaction,
            compositeKey: "TX_001",
            isInsert: true,
            currentVersion: 0,
            deltaJson: null,
            actorId: "gateway");

        // Assert: toVer normalized to at least 1
        Assert.Equal(EMergeClassification.Inserted, audit.OperationType);
        Assert.Equal(0, audit.FromVersion);
        Assert.Equal(1, audit.ToVersion);
        Assert.False(audit.IsNoOp);
    }

    #endregion
}

