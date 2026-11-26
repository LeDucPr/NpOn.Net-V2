using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Receivers;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;
using Common.Extensions.NpOn.Saga;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using ProtoBuf;
using RabbitMQ.Client;
using Xunit;
using Xunit.Abstractions;

namespace NpOn.Core.Tests;

/// <summary>
/// Live PostgreSQL + RabbitMQ stress, atomicity, concurrency, DLQ, and Binary COPY benchmarks for NpOn.Saga.
/// Runs live against local Postgres (5432, DB 'account') and RabbitMQ (5672) when reachable;
/// gracefully degrades with a diagnostic output message if local infrastructure is unavailable (e.g., isolated CI).
/// </summary>
public class SagaPostgresStressTests(ITestOutputHelper output)
{
    private const string PostgresConnStr =
        "Host=localhost;Port=5432;Username=postgres;Password=password;Database=account;Maximum Pool Size=64;Timeout=3;";

    private const string RabbitMqConnStr = "amqp://rabbitmq:password@localhost:5672/";
    private const string OfflineRabbitMqConnStr = "amqp://guest:guest@127.0.0.1:59994/";

    private static async Task<bool> TryEnsureSagaSchemaAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(PostgresConnStr);
            await conn.OpenAsync();
            const string ddl = """
                CREATE TABLE IF NOT EXISTS saga_outbox (
                    outbox_id BIGSERIAL PRIMARY KEY,
                    saga_id UUID NOT NULL,
                    entity_type VARCHAR(64) NOT NULL,
                    entity_key VARCHAR(128) NOT NULL,
                    pre_version BIGINT NOT NULL,
                    post_version BIGINT NOT NULL,
                    sequence_number BIGINT NOT NULL,
                    event_type VARCHAR(128) NOT NULL,
                    payload_json JSONB NOT NULL,
                    status VARCHAR(16) NOT NULL DEFAULT 'NEW',
                    retry_count INT NOT NULL DEFAULT 0,
                    last_error TEXT NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    processed_at TIMESTAMPTZ NULL
                );
                CREATE TABLE IF NOT EXISTS saga_inbox_entity_state (
                    entity_type VARCHAR(64) NOT NULL,
                    key_hash BIGINT NOT NULL,
                    composite_key VARCHAR(128) NOT NULL,
                    last_applied_version BIGINT NOT NULL,
                    last_message_id VARCHAR(128) NOT NULL,
                    is_quarantined BOOLEAN NOT NULL DEFAULT FALSE,
                    retry_count INT NOT NULL DEFAULT 0,
                    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    PRIMARY KEY (entity_type, key_hash)
                );
                CREATE TABLE IF NOT EXISTS saga_inbox_dead_letter (
                    dlq_id BIGSERIAL PRIMARY KEY,
                    message_id VARCHAR(128) NOT NULL,
                    entity_type VARCHAR(64) NOT NULL,
                    key_hash BIGINT NOT NULL,
                    composite_key VARCHAR(128) NOT NULL,
                    failed_version BIGINT NOT NULL,
                    payload_json JSONB NOT NULL,
                    exception_type VARCHAR(256) NOT NULL,
                    exception_message TEXT NOT NULL,
                    retry_count INT NOT NULL,
                    status VARCHAR(32) NOT NULL DEFAULT 'UNRESOLVED',
                    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS saga_entity_audit (
                    audit_id BIGSERIAL PRIMARY KEY,
                    saga_id UUID NOT NULL,
                    entity_type VARCHAR(64) NOT NULL,
                    key_hash BIGINT NOT NULL,
                    composite_key VARCHAR(128) NOT NULL,
                    operation VARCHAR(16) NOT NULL,
                    from_version BIGINT NOT NULL,
                    to_version BIGINT NOT NULL,
                    changed_fields_json JSONB NULL,
                    actor_id VARCHAR(64) NULL,
                    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                """;
            await using var cmd = new NpgsqlCommand(ddl, conn);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task CleanupSagaRowsAsync(string keyPrefix)
    {
        try
        {
            await using var conn = new NpgsqlConnection(PostgresConnStr);
            await conn.OpenAsync();
            string likePattern = $"{keyPrefix}%";
            const string sql = """
                DELETE FROM saga_outbox WHERE entity_key LIKE @pattern;
                DELETE FROM saga_inbox_entity_state WHERE composite_key LIKE @pattern;
                DELETE FROM saga_inbox_dead_letter WHERE composite_key LIKE @pattern;
                DELETE FROM saga_entity_audit WHERE composite_key LIKE @pattern;
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("pattern", likePattern);
            await cmd.ExecuteNonQueryAsync();
        }
        catch
        {
            // Ignore cleanup errors if DB became unavailable.
        }
    }

    private static async Task<int> BinaryCopyAuditsAsync(NpgsqlConnection conn, IReadOnlyList<SagaEntityAudit> audits)
    {
        if (audits.Count == 0) return 0;

        const string copySql =
            "COPY saga_entity_audit (saga_id, entity_type, key_hash, composite_key, operation, from_version, to_version, changed_fields_json, actor_id, created_at) FROM STDIN (FORMAT BINARY)";

        await using var writer = await conn.BeginBinaryImportAsync(copySql);
        foreach (var audit in audits)
        {
            await writer.StartRowAsync();
            await writer.WriteAsync(audit.SagaId, NpgsqlDbType.Uuid);
            await writer.WriteAsync(audit.EntityType.ToString(), NpgsqlDbType.Varchar);
            await writer.WriteAsync(audit.KeyHash, NpgsqlDbType.Bigint);
            await writer.WriteAsync(audit.CompositeKey, NpgsqlDbType.Varchar);
            await writer.WriteAsync(audit.OperationType.ToString().ToUpperInvariant(), NpgsqlDbType.Varchar);
            await writer.WriteAsync(audit.FromVersion, NpgsqlDbType.Bigint);
            await writer.WriteAsync(audit.ToVersion, NpgsqlDbType.Bigint);
            if (string.IsNullOrEmpty(audit.ChangedFieldsJson))
                await writer.WriteNullAsync();
            else
                await writer.WriteAsync(audit.ChangedFieldsJson, NpgsqlDbType.Jsonb);
            await writer.WriteAsync(audit.ActorId, NpgsqlDbType.Varchar);
            await writer.WriteAsync(DateTime.SpecifyKind(audit.CreatedAt, DateTimeKind.Utc), NpgsqlDbType.TimestampTz);
        }

        await writer.CompleteAsync();
        return audits.Count;
    }

    [Fact]
    public async Task S01_HeavyBurstConcurrentWrites_OutboxAndBinaryCopyAudit_ZeroDataLoss()
    {
        if (!await TryEnsureSagaSchemaAsync())
        {
            output.WriteLine("[WARN] Skipping live S01: PostgreSQL on localhost:5432 is not reachable.");
            return;
        }

        const int totalOps = 600;
        const int maxConcurrency = 24;
        string prefix = $"S01_{Guid.NewGuid():N}_";

        try
        {
            using var auditChannel = new SagaAuditChannel(capacity: 10000);
            var throttler = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            var insertedAudits = new ConcurrentBag<SagaEntityAudit>();

            // Dedicated background Binary COPY micro-batch worker draining SagaAuditChannel.
            long totalCopied = 0;
            var copyStopwatch = Stopwatch.StartNew();
            var consumerTask = Task.Run(async () =>
            {
                await using var copyConn = new NpgsqlConnection(PostgresConnStr);
                await copyConn.OpenAsync();
                var buffer = new List<SagaEntityAudit>(256);

                await foreach (var batch in auditChannel.Reader.ReadAllAsync())
                {
                    buffer.AddRange(batch.Audits);
                    while (buffer.Count < 200 && auditChannel.Reader.TryRead(out var nextBatch))
                    {
                        buffer.AddRange(nextBatch.Audits);
                    }

                    if (buffer.Count >= 200)
                    {
                        int written = await BinaryCopyAuditsAsync(copyConn, buffer);
                        Interlocked.Add(ref totalCopied, written);
                        buffer.Clear();
                    }
                }

                if (buffer.Count > 0)
                {
                    int written = await BinaryCopyAuditsAsync(copyConn, buffer);
                    Interlocked.Add(ref totalCopied, written);
                }
            });

            var writeSw = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, totalOps).Select(async i =>
            {
                await throttler.WaitAsync();
                try
                {
                    string entityKey = $"{prefix}{i:D4}";
                    Guid sagaId = IndexerMode.CreateSagaId();
                    long keyHash = SagaKeyHasher.Hash64(entityKey.AsSpan());
                    string payload = $"{{\"seq\":{i},\"status\":\"Active\"}}";

                    await using var conn = new NpgsqlConnection(PostgresConnStr);
                    await conn.OpenAsync();
                    await using var tx = await conn.BeginTransactionAsync();

                    // Upsert domain/inbox state with native PostgreSQL RETURNING (xmax = 0) AS is_insert
                    const string mergeSql = """
                        INSERT INTO saga_inbox_entity_state
                            (entity_type, key_hash, composite_key, last_applied_version, last_message_id, is_quarantined, retry_count)
                        VALUES
                            ('Account', @key_hash, @composite_key, 1, @msg_id, FALSE, 0)
                        ON CONFLICT (entity_type, key_hash) DO UPDATE
                        SET last_applied_version = saga_inbox_entity_state.last_applied_version + 1,
                            last_message_id = EXCLUDED.last_message_id,
                            updated_at = CURRENT_TIMESTAMP
                        RETURNING (xmax = 0) AS is_insert, last_applied_version;
                        """;

                    bool isInsert;
                    long currentVersion;
                    await using (var mergeCmd = new NpgsqlCommand(mergeSql, conn, tx))
                    {
                        mergeCmd.Parameters.AddWithValue("key_hash", keyHash);
                        mergeCmd.Parameters.AddWithValue("composite_key", entityKey);
                        mergeCmd.Parameters.AddWithValue("msg_id", sagaId.ToString());
                        await using var reader = await mergeCmd.ExecuteReaderAsync();
                        Assert.True(await reader.ReadAsync());
                        isInsert = reader.GetBoolean(0);
                        currentVersion = reader.GetInt64(1);
                    }

                    const string outboxSql = """
                        INSERT INTO saga_outbox
                            (saga_id, entity_type, entity_key, pre_version, post_version, sequence_number, event_type, payload_json, status)
                        VALUES
                            (@saga_id, 'Account', @entity_key, @pre_ver, @post_ver, @seq, 'SagaBurstEvent', @payload::jsonb, 'NEW');
                        """;
                    await using (var outboxCmd = new NpgsqlCommand(outboxSql, conn, tx))
                    {
                        outboxCmd.Parameters.AddWithValue("saga_id", sagaId);
                        outboxCmd.Parameters.AddWithValue("entity_key", entityKey);
                        outboxCmd.Parameters.AddWithValue("pre_ver", currentVersion - 1);
                        outboxCmd.Parameters.AddWithValue("post_ver", currentVersion);
                        outboxCmd.Parameters.AddWithValue("seq", currentVersion);
                        outboxCmd.Parameters.AddWithValue("payload", payload);
                        await outboxCmd.ExecuteNonQueryAsync();
                    }

                    await tx.CommitAsync();

                    // Push lock-free after ACID commit
                    var audit = SagaMergeFusionEngine.FuseSingle(
                        ESagaEntityType.Account,
                        entityKey,
                        isInsert,
                        currentVersion,
                        payload,
                        "stress_worker");
                    audit.SagaId = sagaId;
                    insertedAudits.Add(audit);
                    await auditChannel.WriteAsync(new SagaAuditBatch([audit]));
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks);
            writeSw.Stop();

            auditChannel.Complete();
            await consumerTask;
            copyStopwatch.Stop();

            await using var verifyConn = new NpgsqlConnection(PostgresConnStr);
            await verifyConn.OpenAsync();

            long outboxCount;
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM saga_outbox WHERE entity_key LIKE @p", verifyConn))
            {
                cmd.Parameters.AddWithValue("p", $"{prefix}%");
                outboxCount = (long)(await cmd.ExecuteScalarAsync())!;
            }

            long auditCount;
            await using (var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM saga_entity_audit WHERE composite_key LIKE @p", verifyConn))
            {
                cmd.Parameters.AddWithValue("p", $"{prefix}%");
                auditCount = (long)(await cmd.ExecuteScalarAsync())!;
            }

            Assert.Equal(totalOps, outboxCount);
            Assert.Equal(totalOps, totalCopied);
            Assert.Equal(totalOps, auditCount);

            double writeOpsPerSec = totalOps / writeSw.Elapsed.TotalSeconds;
            double copyRecPerSec = totalOps / copyStopwatch.Elapsed.TotalSeconds;
            output.WriteLine(
                $"[S01 LIVE BENCH] {totalOps} concurrent tx @ conc={maxConcurrency}: " +
                $"WriteThroughput={writeOpsPerSec:F1} tx/s ({writeSw.ElapsedMilliseconds}ms), " +
                $"BinaryCopy={copyRecPerSec:F1} rec/s ({copyStopwatch.ElapsedMilliseconds}ms)");
        }
        finally
        {
            await CleanupSagaRowsAsync(prefix);
        }
    }

    [Fact]
    public async Task S02_TransactionRollbackIntegrity_MidTxFailure_LeavesZeroOrphanOutboxOrAudit()
    {
        if (!await TryEnsureSagaSchemaAsync())
        {
            output.WriteLine("[WARN] Skipping live S02: PostgreSQL on localhost:5432 is not reachable.");
            return;
        }

        string prefix = $"S02_{Guid.NewGuid():N}_";
        string entityKey = $"{prefix}ORD_01";
        long keyHash = SagaKeyHasher.Hash64(entityKey.AsSpan());

        try
        {
            await using var conn = new NpgsqlConnection(PostgresConnStr);
            await conn.OpenAsync();

            // Seed committed v1 entity state.
            const string seedSql = """
                INSERT INTO saga_inbox_entity_state
                    (entity_type, key_hash, composite_key, last_applied_version, last_message_id, is_quarantined, retry_count)
                VALUES
                    ('Account', @kh, @ck, 1, 'seed-msg', FALSE, 0);
                """;
            await using (var seedCmd = new NpgsqlCommand(seedSql, conn))
            {
                seedCmd.Parameters.AddWithValue("kh", keyHash);
                seedCmd.Parameters.AddWithValue("ck", entityKey);
                await seedCmd.ExecuteNonQueryAsync();
            }

            using var auditChannel = new SagaAuditChannel(capacity: 100);

            // Attempt tx2 that updates business state + inserts saga_outbox BEFORE hitting duplicate PK violation -> rolls back.
            await using (var tx = await conn.BeginTransactionAsync())
            {
                await using (var updateCmd = new NpgsqlCommand(
                    "UPDATE saga_inbox_entity_state SET last_applied_version = 2 WHERE entity_type = 'Account' AND key_hash = @kh;",
                    conn, tx))
                {
                    updateCmd.Parameters.AddWithValue("kh", keyHash);
                    await updateCmd.ExecuteNonQueryAsync();
                }

                await using (var outboxCmd = new NpgsqlCommand(
                    "INSERT INTO saga_outbox (saga_id, entity_type, entity_key, pre_version, post_version, sequence_number, event_type, payload_json, status) " +
                    "VALUES (@sid, 'Account', @ek, 1, 2, 2, 'ShouldRollbackEvent', '{\"x\":1}'::jsonb, 'NEW');",
                    conn, tx))
                {
                    outboxCmd.Parameters.AddWithValue("sid", IndexerMode.CreateSagaId());
                    outboxCmd.Parameters.AddWithValue("ek", entityKey);
                    await outboxCmd.ExecuteNonQueryAsync();
                }

                // Simulate mid-transaction constraint violation (duplicate PK on saga_inbox_entity_state).
                await Assert.ThrowsAsync<PostgresException>(async () =>
                {
                    await using var failCmd = new NpgsqlCommand(seedSql, conn, tx);
                    failCmd.Parameters.AddWithValue("kh", keyHash);
                    failCmd.Parameters.AddWithValue("ck", entityKey);
                    await failCmd.ExecuteNonQueryAsync();
                });

                await tx.RollbackAsync();
                // Audit channel must NOT receive batches from rolled-back transactions.
            }

            Assert.Equal(0, auditChannel.Count);

            long versionAfterRollback;
            await using (var checkVerCmd = new NpgsqlCommand(
                "SELECT last_applied_version FROM saga_inbox_entity_state WHERE entity_type = 'Account' AND key_hash = @kh;",
                conn))
            {
                checkVerCmd.Parameters.AddWithValue("kh", keyHash);
                versionAfterRollback = (long)(await checkVerCmd.ExecuteScalarAsync())!;
            }

            long orphanOutbox;
            await using (var checkOutboxCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM saga_outbox WHERE entity_key = @ek;", conn))
            {
                checkOutboxCmd.Parameters.AddWithValue("ek", entityKey);
                orphanOutbox = (long)(await checkOutboxCmd.ExecuteScalarAsync())!;
            }

            long orphanAudit;
            await using (var checkAuditCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM saga_entity_audit WHERE composite_key = @ek;", conn))
            {
                checkAuditCmd.Parameters.AddWithValue("ek", entityKey);
                orphanAudit = (long)(await checkAuditCmd.ExecuteScalarAsync())!;
            }

            Assert.Equal(1L, versionAfterRollback);
            Assert.Equal(0L, orphanOutbox);
            Assert.Equal(0L, orphanAudit);
        }
        finally
        {
            await CleanupSagaRowsAsync(prefix);
        }
    }

    [Fact]
    public async Task S03_InboxIdempotency_ConcurrentVersionConflict_And_DlqQuarantine()
    {
        if (!await TryEnsureSagaSchemaAsync())
        {
            output.WriteLine("[WARN] Skipping live S03: PostgreSQL on localhost:5432 is not reachable.");
            return;
        }

        string prefix = $"S03_{Guid.NewGuid():N}_";
        string entityKey = $"{prefix}SHARED_ENTITY";
        long keyHash = SagaKeyHasher.Hash64(entityKey.AsSpan());

        try
        {
            await using var initConn = new NpgsqlConnection(PostgresConnStr);
            await initConn.OpenAsync();
            const string seedSql = """
                INSERT INTO saga_inbox_entity_state
                    (entity_type, key_hash, composite_key, last_applied_version, last_message_id, is_quarantined, retry_count)
                VALUES
                    ('Account', @kh, @ck, 0, 'init', FALSE, 0);
                """;
            await using (var cmd = new NpgsqlCommand(seedSql, initConn))
            {
                cmd.Parameters.AddWithValue("kh", keyHash);
                cmd.Parameters.AddWithValue("ck", entityKey);
                await cmd.ExecuteNonQueryAsync();
            }

            // Hammer the SAME entity concurrently with consecutive steps, duplicates, out-of-order jumps, and stale conflicts.
            var candidates = new List<(long Pre, long Post, string MsgId)>();
            for (long v = 0; v < 10; v++)
            {
                candidates.Add((v, v + 1, $"msg-{v + 1}"));
                candidates.Add((v, v + 1, $"dup-{v + 1}")); // duplicate delivery
            }
            for (int j = 0; j < 15; j++)
            {
                candidates.Add((50 + j, 51 + j, $"ooo-{j}")); // out-of-order gap
                candidates.Add((0, 99, $"conflict-{j}"));     // conflicting rewrite
            }

            int appliedCount = 0;
            var applyLock = new SemaphoreSlim(1, 1);
            await Task.WhenAll(candidates.Select(async item =>
            {
                // Run concurrent DB CAS attempts against Postgres + Inbox state guard.
                await using var conn = new NpgsqlConnection(PostgresConnStr);
                await conn.OpenAsync();

                long currentVer;
                bool quarantined;
                await using (var readCmd = new NpgsqlCommand(
                    "SELECT last_applied_version, is_quarantined FROM saga_inbox_entity_state WHERE entity_type = 'Account' AND key_hash = @kh;",
                    conn))
                {
                    readCmd.Parameters.AddWithValue("kh", keyHash);
                    await using var r = await readCmd.ExecuteReaderAsync();
                    Assert.True(await r.ReadAsync());
                    currentVer = r.GetInt64(0);
                    quarantined = r.GetBoolean(1);
                }

                var memState = new SagaInboxEntityState
                {
                    EntityType = ESagaEntityType.Account,
                    KeyHash = keyHash,
                    CompositeKey = entityKey,
                    LastAppliedVersion = currentVer,
                    IsQuarantined = quarantined
                };

                var eval = memState.EvaluateTransitionWithRetry(item.Pre, item.Post, currentRetryCount: 0, maxRetries: 3);
                if (eval != ESagaInboxProcessingResult.Applied)
                    return;

                // Atomic SQL version guard prevents race downgrade/duplicate apply even across concurrent threads.
                const string casSql = """
                    UPDATE saga_inbox_entity_state
                    SET last_applied_version = @post_ver,
                        last_message_id = @msg_id,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE entity_type = 'Account'
                      AND key_hash = @kh
                      AND last_applied_version = @pre_ver
                      AND is_quarantined = FALSE;
                    """;
                await using var casCmd = new NpgsqlCommand(casSql, conn);
                casCmd.Parameters.AddWithValue("post_ver", item.Post);
                casCmd.Parameters.AddWithValue("msg_id", item.MsgId);
                casCmd.Parameters.AddWithValue("kh", keyHash);
                casCmd.Parameters.AddWithValue("pre_ver", item.Pre);

                int affected = await casCmd.ExecuteNonQueryAsync();
                if (affected == 1)
                {
                    await applyLock.WaitAsync();
                    try { appliedCount++; }
                    finally { applyLock.Release(); }
                }
            }));

            long finalVersion;
            await using (var finalCmd = new NpgsqlCommand(
                "SELECT last_applied_version FROM saga_inbox_entity_state WHERE entity_type = 'Account' AND key_hash = @kh;",
                initConn))
            {
                finalCmd.Parameters.AddWithValue("kh", keyHash);
                finalVersion = (long)(await finalCmd.ExecuteScalarAsync())!;
            }

            // Strictly monotonic: appliedCount must equal finalVersion (never > 10, never downgraded).
            Assert.InRange(finalVersion, 1, 10);
            Assert.Equal(appliedCount, finalVersion);

            // Poison message retry exhaustion -> DeadLetter + quarantine in DB
            var poisonState = new SagaInboxEntityState
            {
                EntityType = ESagaEntityType.Account,
                KeyHash = keyHash,
                CompositeKey = entityKey,
                LastAppliedVersion = finalVersion
            };
            Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder,
                poisonState.EvaluateTransitionWithRetry(finalVersion + 5, finalVersion + 6, currentRetryCount: 1, maxRetries: 3));
            Assert.Equal(ESagaInboxProcessingResult.DeferredOutOfOrder,
                poisonState.EvaluateTransitionWithRetry(finalVersion + 5, finalVersion + 6, currentRetryCount: 2, maxRetries: 3));
            var dlqResult = poisonState.EvaluateTransitionWithRetry(
                finalVersion + 5, finalVersion + 6, currentRetryCount: 3, maxRetries: 3);
            Assert.Equal(ESagaInboxProcessingResult.DeadLetter, dlqResult);
            Assert.True(poisonState.IsQuarantined);

            const string dlqSql = """
                UPDATE saga_inbox_entity_state
                SET is_quarantined = TRUE, retry_count = 3, updated_at = CURRENT_TIMESTAMP
                WHERE entity_type = 'Account' AND key_hash = @kh;

                INSERT INTO saga_inbox_dead_letter
                    (message_id, entity_type, key_hash, composite_key, failed_version, payload_json, exception_type, exception_message, retry_count, status)
                VALUES
                    ('poison-msg-1', 'Account', @kh, @ck, @fv, '{"poison":true}'::jsonb, 'MaxRetryExceededException', 'Deferred gap exceeded 3 retries', 3, 'UNRESOLVED');
                """;
            await using (var dlqCmd = new NpgsqlCommand(dlqSql, initConn))
            {
                dlqCmd.Parameters.AddWithValue("kh", keyHash);
                dlqCmd.Parameters.AddWithValue("ck", entityKey);
                dlqCmd.Parameters.AddWithValue("fv", finalVersion + 6);
                await dlqCmd.ExecuteNonQueryAsync();
            }

            // Quarantined entity must refuse even a valid next-version message in both C# guard and SQL CAS.
            Assert.Equal(ESagaInboxProcessingResult.DeadLetter,
                poisonState.EvaluateTransitionWithRetry(finalVersion, finalVersion + 1, currentRetryCount: 0, maxRetries: 3));

            await using var checkQuarantineCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM saga_inbox_dead_letter WHERE composite_key = @ck AND status = 'UNRESOLVED';",
                initConn);
            checkQuarantineCmd.Parameters.AddWithValue("ck", entityKey);
            long dlqCount = (long)(await checkQuarantineCmd.ExecuteScalarAsync())!;
            Assert.Equal(1L, dlqCount);

            output.WriteLine($"[S03 LIVE] FinalVersion={finalVersion}, AppliedCount={appliedCount}, DLQ={dlqCount}");
        }
        finally
        {
            await CleanupSagaRowsAsync(prefix);
        }
    }

    [Fact]
    public async Task S04_LiveBrokerOutage_OutboxBurst_And_RabbitMqDrainRecovery()
    {
        if (!await TryEnsureSagaSchemaAsync())
        {
            output.WriteLine("[WARN] Skipping live S04: PostgreSQL on localhost:5432 is not reachable.");
            return;
        }

        // Probe live RabbitMQ broker availability first.
        bool rabbitAvailable = false;
        try
        {
            var probeFactory = new ConnectionFactory { Uri = new Uri(RabbitMqConnStr), RequestedConnectionTimeout = TimeSpan.FromSeconds(2) };
            await using var probeConn = await probeFactory.CreateConnectionAsync();
            rabbitAvailable = probeConn.IsOpen;
        }
        catch
        {
            rabbitAvailable = false;
        }

        if (!rabbitAvailable)
        {
            output.WriteLine("[WARN] Skipping live S04: RabbitMQ on localhost:5672 is not reachable.");
            return;
        }

        const int burstCount = 50;
        string runId = Guid.NewGuid().ToString("N")[..12];
        string prefix = $"S04_{runId}_";
        string exchangeName = $"NpOn.SagaStress.{runId}";
        string routeKey = $"saga.stress.{runId}";

        try
        {
            // Phase 1: Simulate broker unreachable -> producer must not crash; Outbox persists safely in Postgres.
            using (var offlineConn = new RabbitMqConnection(OfflineRabbitMqConnStr, exchangeName))
            {
                Assert.False(offlineConn.IsConnected);
                var offlineProducer = new RabbitMqProducer(offlineConn, NullLogger<RabbitMqProducer>.Instance);
                offlineProducer.AddEvent(new RabbitMqEvent<SagaOutboxDispatchMessage>
                {
                    MessageContent = new SagaOutboxDispatchMessage { SagaId = Guid.NewGuid().ToString(), EntityKey = "offline_probe" }
                });
                await Task.Delay(150);
            }

            await using var pgConn = new NpgsqlConnection(PostgresConnStr);
            await pgConn.OpenAsync();
            for (int i = 0; i < burstCount; i++)
            {
                Guid sagaId = IndexerMode.CreateSagaId();
                string entityKey = $"{prefix}{i:D3}";
                const string insertSql = """
                    INSERT INTO saga_outbox
                        (saga_id, entity_type, entity_key, pre_version, post_version, sequence_number, event_type, payload_json, status, retry_count, last_error)
                    VALUES
                        (@sid, 'Account', @ek, 0, 1, 1, 'OutageBufferedEvent', @payload::jsonb, 'NEW', 1, 'Broker offline during burst');
                    """;
                await using var cmd = new NpgsqlCommand(insertSql, pgConn);
                cmd.Parameters.AddWithValue("sid", sagaId);
                cmd.Parameters.AddWithValue("ek", entityKey);
                cmd.Parameters.AddWithValue("payload", JsonSerializer.Serialize(new { entityKey, index = i }));
                await cmd.ExecuteNonQueryAsync();
            }

            // Phase 2: Broker recovers -> relay reads NEW outbox rows, publishes via live RabbitMQ (+ injects duplicates), consumer applies idempotently!
            using var liveConn = new RabbitMqConnection(RabbitMqConnStr, exchangeName);
            Assert.True(await liveConn.EnsureConnectedAsync());

            var processedDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var uniqueAppliedCount = 0;

            using var consumer = new SagaRelayTestConsumer(
                liveConn,
                routeKey,
                async msg =>
                {
                    await using var c = new NpgsqlConnection(PostgresConnStr);
                    await c.OpenAsync();
                    long kh = SagaKeyHasher.Hash64(msg.EntityKey.AsSpan());

                    const string inboxUpsert = """
                        INSERT INTO saga_inbox_entity_state
                            (entity_type, key_hash, composite_key, last_applied_version, last_message_id, is_quarantined, retry_count)
                        VALUES
                            ('Account', @kh, @ck, 1, @mid, FALSE, 0)
                        ON CONFLICT (entity_type, key_hash) DO NOTHING;
                        """;
                    await using var upsertCmd = new NpgsqlCommand(inboxUpsert, c);
                    upsertCmd.Parameters.AddWithValue("kh", kh);
                    upsertCmd.Parameters.AddWithValue("ck", msg.EntityKey);
                    upsertCmd.Parameters.AddWithValue("mid", msg.SagaId);
                    int inserted = await upsertCmd.ExecuteNonQueryAsync();
                    if (inserted == 1)
                    {
                        if (Interlocked.Increment(ref uniqueAppliedCount) >= burstCount)
                            processedDone.TrySetResult(true);
                    }
                });

            var liveProducer = new RabbitMqProducer(liveConn, NullLogger<RabbitMqProducer>.Instance);
            var outboxItems = new List<(long OutboxId, string SagaId, string EntityKey)>(burstCount);
            await using (var selectCmd = new NpgsqlCommand(
                "SELECT outbox_id, saga_id, entity_key FROM saga_outbox WHERE entity_key LIKE @p AND status = 'NEW' ORDER BY outbox_id;",
                pgConn))
            {
                selectCmd.Parameters.AddWithValue("p", $"{prefix}%");
                await using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    outboxItems.Add((reader.GetInt64(0), reader.GetGuid(1).ToString(), reader.GetString(2)));
                }
            }

            Assert.Equal(burstCount, outboxItems.Count);

            // Publish all 50 + 10 intentional duplicate deliveries directly on the relay routeKey/queue.
            var props = new BasicProperties { Persistent = true };
            foreach (var item in outboxItems)
            {
                byte[] body = ProtoBufMode.ProtoBufSerialize(new RabbitMqEvent<SagaOutboxDispatchMessage>
                {
                    MessageContent = new SagaOutboxDispatchMessage { SagaId = item.SagaId, EntityKey = item.EntityKey }
                });
                await liveConn.Channel.BasicPublishAsync(exchangeName, routeKey, mandatory: false, props, body);
            }
            for (int d = 0; d < 10; d++)
            {
                var dup = outboxItems[d];
                byte[] body = ProtoBufMode.ProtoBufSerialize(new RabbitMqEvent<SagaOutboxDispatchMessage>
                {
                    MessageContent = new SagaOutboxDispatchMessage { SagaId = dup.SagaId, EntityKey = dup.EntityKey }
                });
                await liveConn.Channel.BasicPublishAsync(exchangeName, routeKey, mandatory: false, props, body);
            }
            _ = liveProducer;

            var completed = await Task.WhenAny(processedDone.Task, Task.Delay(TimeSpan.FromSeconds(15)));
            Assert.Same(processedDone.Task, completed);

            // Give duplicates a short settle window to confirm no duplicate rows enter Postgres.
            await Task.Delay(400);

            await using (var markPublished = new NpgsqlCommand(
                "UPDATE saga_outbox SET status = 'PUBLISHED', processed_at = CURRENT_TIMESTAMP, last_error = NULL WHERE entity_key LIKE @p AND status = 'NEW';",
                pgConn))
            {
                markPublished.Parameters.AddWithValue("p", $"{prefix}%");
                await markPublished.ExecuteNonQueryAsync();
            }

            long publishedCount;
            await using (var pubCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM saga_outbox WHERE entity_key LIKE @p AND status = 'PUBLISHED';", pgConn))
            {
                pubCmd.Parameters.AddWithValue("p", $"{prefix}%");
                publishedCount = (long)(await pubCmd.ExecuteScalarAsync())!;
            }

            long inboxCount;
            await using (var inbCmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM saga_inbox_entity_state WHERE composite_key LIKE @p AND last_applied_version = 1;", pgConn))
            {
                inbCmd.Parameters.AddWithValue("p", $"{prefix}%");
                inboxCount = (long)(await inbCmd.ExecuteScalarAsync())!;
            }

            Assert.Equal(burstCount, publishedCount);
            Assert.Equal(burstCount, inboxCount);
            Assert.Equal(burstCount, uniqueAppliedCount);

            // Delete temporary per-test queue & exchange cleanly from RabbitMQ.
            try
            {
                await liveConn.Channel.QueueDeleteAsync(consumer.QueueName);
                await liveConn.Channel.ExchangeDeleteAsync(exchangeName);
            }
            catch
            {
                // Ignore cleanup failure on broker shutdown.
            }

            output.WriteLine($"[S04 LIVE BROKER DRAIN] Published={publishedCount}, InboxApplied={inboxCount} (10 dupes ignored)");
        }
        finally
        {
            await CleanupSagaRowsAsync(prefix);
        }
    }

    [ProtoContract]
    private sealed class SagaOutboxDispatchMessage : CommonMessageContent
    {
        [ProtoMember(1)] public string SagaId { get; set; } = string.Empty;
        [ProtoMember(2)] public string EntityKey { get; set; } = string.Empty;
    }

    private sealed class SagaRelayTestConsumer(
        IRabbitMqConnection conn,
        string routeKey,
        Func<SagaOutboxDispatchMessage, Task> onMessage)
        : RabbitMqConsumer<SagaOutboxDispatchMessage>(
            conn,
            NullLogger<RabbitMqConsumer<SagaOutboxDispatchMessage>>.Instance,
            autoAck: true,
            prefetchCount: 32,
            exchangeType: ERabbitMqExchangeType.Direct,
            routeKey: routeKey)
    {
        public override void AddHandler() => Handler = onMessage;
    }
}
