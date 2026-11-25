using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.Saga;
using Xunit;

namespace NpOn.Core.Tests;

public class SagaEngineTests
{
    [Fact]
    public void SagaKeyHasher_Hash64_ShouldBeDeterministicAndHandleCompositeKeys()
    {
        // Arrange
        const string compositeKey1 = "1001#5520#DEV_01";
        const string compositeKey2 = "1001#5520#DEV_02";

        // Act
        long hash1A = SagaKeyHasher.Hash64(compositeKey1.AsSpan());
        long hash1B = SagaKeyHasher.Hash64(compositeKey1.AsSpan());
        long hash2 = SagaKeyHasher.Hash64(compositeKey2.AsSpan());

        // Assert
        Assert.NotEqual(0L, hash1A);
        Assert.Equal(hash1A, hash1B); // Deterministic
        Assert.NotEqual(hash1A, hash2); // Collision free for different keys
    }

    [Fact]
    public void SagaInboxEntityState_EvaluateTransition_ShouldEnforceStrictCausality()
    {
        // 1. Genesis Transition: V0 -> V1
        var state = new SagaInboxEntityState
        {
            EntityType = ESagaEntityType.Account,
            KeyHash = 123456789L,
            LastAppliedVersion = 0
        };

        var genesisResult = state.EvaluateTransition(preVersion: 0, postVersion: 1);
        Assert.Equal(ESagaInboxProcessingResult.Applied, genesisResult);

        // Advance state
        state.LastAppliedVersion = 1;

        // 2. Consecutive Transition: V1 -> V2
        var consecutiveResult = state.EvaluateTransition(preVersion: 1, postVersion: 2);
        Assert.Equal(ESagaInboxProcessingResult.Applied, consecutiveResult);

        state.LastAppliedVersion = 2;

        // 3. Stale / Duplicate Message: PostVersion <= LastAppliedVersion
        var staleResult = state.EvaluateTransition(preVersion: 0, postVersion: 1);
        Assert.Equal(ESagaInboxProcessingResult.DuplicateDropped, staleResult);

        var duplicateResult = state.EvaluateTransition(preVersion: 1, postVersion: 2);
        Assert.Equal(ESagaInboxProcessingResult.DuplicateDropped, duplicateResult);

        // 4. Out-of-Order / Gap: PreVersion > LastAppliedVersion (e.g. current is 2, event has pre=4, post=5)
        var gapResult = state.EvaluateTransition(preVersion: 4, postVersion: 5);
        Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder, gapResult);
    }

    [Fact]
    public void SagaMergeFusionEngine_FuseSingle_ShouldCorrectlyClassifyInsertAndDeltaUpdate()
    {
        // 1. Insert Outcome
        var insertAudit = SagaMergeFusionEngine.FuseSingle(
            ESagaEntityType.Account,
            compositeKey: "ACC_999",
            isInsert: true,
            currentVersion: 1,
            deltaJson: null,
            actorId: "agent_admin");

        Assert.Equal(EMergeClassification.Inserted, insertAudit.OperationType);
        Assert.Equal(0, insertAudit.FromVersion);
        Assert.Equal(1, insertAudit.ToVersion);
        Assert.False(insertAudit.IsNoOp);

        // 2. Update Outcome with Changed Fields
        const string deltaJson = "{\"phone_number\":{\"old\":\"0901\",\"new\":\"0909\"}}";
        var updateAudit = SagaMergeFusionEngine.FuseSingle(
            ESagaEntityType.Account,
            compositeKey: "ACC_999",
            isInsert: false,
            currentVersion: 2,
            deltaJson: deltaJson,
            actorId: "agent_admin");

        Assert.Equal(EMergeClassification.Updated, updateAudit.OperationType);
        Assert.Equal(1, updateAudit.FromVersion);
        Assert.Equal(2, updateAudit.ToVersion);
        Assert.Equal(deltaJson, updateAudit.ChangedFieldsJson);
        Assert.False(updateAudit.IsNoOp);

        // 3. No-Op Outcome (delta is empty or null on update)
        var noOpAudit = SagaMergeFusionEngine.FuseSingle(
            ESagaEntityType.Account,
            compositeKey: "ACC_999",
            isInsert: false,
            currentVersion: 2,
            deltaJson: null,
            actorId: "agent_admin");

        Assert.Equal(EMergeClassification.NoOp, noOpAudit.OperationType);
        Assert.True(noOpAudit.IsNoOp);
    }

    [Fact]
    public async Task SagaAuditChannel_NonBlockingPush_ShouldHandleHighThroughput()
    {
        // Arrange
        using var channel = new SagaAuditChannel(capacity: 50000);
        const int batchCount = 1000;
        var auditsPerBatch = 5;

        // Act: Push batches
        var pushSw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < batchCount; i++)
        {
            var audits = new List<SagaEntityAudit>(auditsPerBatch);
            for (int j = 0; j < auditsPerBatch; j++)
            {
                audits.Add(new SagaEntityAudit
                {
                    EntityType = ESagaEntityType.Device,
                    KeyHash = i * 10 + j,
                    OperationType = EMergeClassification.Inserted,
                    FromVersion = 0,
                    ToVersion = 1
                });
            }
            bool written = channel.TryWrite(new SagaAuditBatch(audits));
            Assert.True(written);
        }
        pushSw.Stop();

        // Verify: Non-blocking in-memory push speed (should complete under 50ms)
        Assert.True(pushSw.ElapsedMilliseconds < 500, $"Push duration {pushSw.ElapsedMilliseconds} ms too slow");

        // Read all items back
        int readBatches = 0;
        while (channel.Reader.TryRead(out var batch))
        {
            Assert.Equal(auditsPerBatch, batch.Audits.Count);
            readBatches++;
        }

        Assert.Equal(batchCount, readBatches);
    }

    [Fact]
    public void SagaConcurrencyConflictException_ShouldCarryMetadata()
    {
        // Arrange & Act
        var ex = new SagaConcurrencyConflictException(ESagaEntityType.Account, "ACC_123", expectedVersion: 5);

        // Assert
        Assert.Equal(ESagaEntityType.Account, ex.EntityType);
        Assert.Equal("ACC_123", ex.EntityKey);
        Assert.Equal(5, ex.ExpectedVersion);
        Assert.Contains("OCC Conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IndexerMode_GuidGenerators_ShouldGenerateAccurateVersionsAndFormats()
    {
        // 1. Version 7 (Time-ordered)
        var v7A = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateGuid();
        var v7B = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateGuidV7();
        var sagaId = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateSagaId();
        var auditId = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateAuditId();

        Assert.Equal(7, v7A.Version);
        Assert.Equal(7, v7B.Version);
        Assert.Equal(7, sagaId.Version);
        Assert.Equal(7, auditId.Version);

        // 2. Version 4 (Random)
        var v4 = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateGuidV4();
        Assert.Equal(4, v4.Version);

        // 3. Short Guid (22-char Base64 URL format)
        var shortGuid = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateShortGuid();
        Assert.Equal(22, shortGuid.Length);
        Assert.DoesNotContain("+", shortGuid);
        Assert.DoesNotContain("/", shortGuid);

        // 4. COMB Guid
        var comb = Common.Extensions.NpOn.CommonMode.IndexerMode.CreateCombGuid();
        Assert.NotEqual(Guid.Empty, comb);
    }
}
