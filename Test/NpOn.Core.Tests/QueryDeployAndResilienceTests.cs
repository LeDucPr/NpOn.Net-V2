using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.BaseDbFactory.Broadcasts;
using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HeaderConfig;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Receivers;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using Grpc.Core;
using Grpc.Core.Interceptors;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Service.NpOn.AccountService.Services;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using MicroServices.General.Service.NpOn.GeneralService.Mappers;
using MicroServices.General.Service.NpOn.GeneralService.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NpOn.CommonGrpcCall;
using Npgsql;
using NpgsqlTypes;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using ProtoGeneral = NpOn.Protos.General.V1;

namespace NpOn.Core.Tests;

public class QueryDeployAndResilienceTests
{
    private static void EnsureCachingEnabledConfig()
    {
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IsUseCachingExecutionGeneralService"] = "true"
            })
            .Build()
            .InitConfigs(typeof(EApplicationConfiguration));
    }

    [Fact]
    public async Task Q01_FldMasterService_GetExecCommand_UsesCanonicalStringCacheKey_HitsRepeatedNewInstances()
    {
        EnsureCachingEnabledConfig();

        var cache = new WrapperCacheStore<string, List<TblFldRModel>>();
        var pg = new FakeGeneralPostgresFactory();
        var tblId = IndexerMode.CreateGuid();
        pg.SeedQuery(
            tblId,
            "q_user_by_id",
            "SELECT * FROM users WHERE id = @id",
            EExecType.Query,
            EDb.Postgres,
            [("id", (int)NpgsqlDbType.Uuid, "NpgsqlDbType.Uuid", 0)]);

        var service = new FldMasterService(cache, pg, NullLogger<CommonService>.Instance);

        var first = await service.GetExecCommand(new TblFldExecutionCommand("q_user_by_id"));
        var second = await service.GetExecCommand(new TblFldExecutionCommand { Code = "q_user_by_id" });

        Assert.True(first.Status);
        Assert.NotNull(first.Data);
        Assert.True(second.Status);
        Assert.NotNull(second.Data);
        Assert.Equal("SELECT * FROM users WHERE id = @id", second.Data.CommandText);
        Assert.Equal(1, pg.SelectCount);
        Assert.True(cache.TryGetValue("code:q_user_by_id", out var cachedRows));
        Assert.NotNull(cachedRows);
        Assert.Single(cachedRows);
    }

    [Fact]
    public async Task Q02_FldMasterService_BulkSyncQueries_SingleTransaction_WriteThroughCache_NoClear_OneBroadcast()
    {
        EnsureCachingEnabledConfig();

        var cache = new WrapperCacheStore<string, List<TblFldRModel>>();
        var untouchedEntry = new List<TblFldRModel>
        {
            new() { TblMaterId = Guid.NewGuid(), TblMaterCode = "untouched_query", Query = "SELECT 1", ExecType = EExecType.Query, DataBaseType = EDb.Postgres }
        };
        cache.AddOrUpdate("code:untouched_query", untouchedEntry, TimeSpan.FromMinutes(10));

        var pg = new FakeGeneralPostgresFactory();
        var broadcaster = new SpyRedisBroadcastFactoryWrapper();
        var service = new FldMasterService(cache, pg, NullLogger<CommonService>.Instance, broadcaster);

        var batch = new BulkSyncQueriesCommand
        {
            DeployVersion = "deploy-001",
            IsFullReload = false,
            Queries =
            [
                new TblMasterSyncItemCommand
                {
                    Code = "query_alpha",
                    Query = "SELECT * FROM alpha WHERE a = @a",
                    ExecType = (int)EExecType.Query,
                    DbType = (int)EDb.Postgres,
                    Fields =
                    [
                        new FldQueryMasterSyncCommand { FieldName = "a", FieldType = (int)NpgsqlDbType.Varchar, OrderSort = 0 }
                    ]
                },
                new TblMasterSyncItemCommand
                {
                    Code = "query_beta",
                    Query = "SELECT * FROM beta WHERE b1 = @b1 AND b2 = @b2",
                    ExecType = (int)EExecType.Query,
                    DbType = (int)EDb.Postgres,
                    Fields =
                    [
                        new FldQueryMasterSyncCommand { FieldName = "b1", FieldType = (int)NpgsqlDbType.Uuid, OrderSort = 0 },
                        new FldQueryMasterSyncCommand { FieldName = "b2", FieldType = (int)NpgsqlDbType.Integer, OrderSort = 1 }
                    ]
                }
            ]
        };

        var result = await service.BulkSyncQueries(batch);

        Assert.True(result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.SyncedCount);
        Assert.Equal("deploy-001", result.Data.DeployVersion);
        Assert.Equal(3, result.Data.BroadcastReceivers);
        Assert.Equal(1, pg.TransactionCount);

        // Cache was updated via AddOrUpdate without Clear(): untouched key remains intact.
        Assert.True(cache.TryGetValue("code:untouched_query", out _));
        Assert.True(cache.TryGetValue("code:query_alpha", out var alphaRows));
        Assert.NotNull(alphaRows);
        Assert.Equal("SELECT * FROM alpha WHERE a = @a", alphaRows[0].Query);

        Assert.Single(broadcaster.PublishedMessages);
        var published = Assert.IsType<RedisBroadcastMessage<GeneralQuerySyncBroadcastMessage>>(broadcaster.PublishedMessages[0]);
        Assert.Equal(GeneralQuerySyncBroadcastConstant.GeneralQuerySyncChannel, published.Channel);
        Assert.NotNull(published.Value);
        Assert.Equal(["query_alpha", "query_beta"], published.Value.Codes!);
    }

    [Fact]
    public async Task Q03_FldMasterService_BulkSyncQueries_ConcurrentReadersDuringSwap_ZeroErrorsAndNoPartialParams()
    {
        EnsureCachingEnabledConfig();

        var cache = new WrapperCacheStore<string, List<TblFldRModel>>();
        var pg = new FakeGeneralPostgresFactory();
        var tblId = IndexerMode.CreateGuid();
        pg.SeedQuery(
            tblId,
            "hot_query",
            "SELECT v1 FROM t WHERE id = @id",
            EExecType.Query,
            EDb.Postgres,
            [("id", (int)NpgsqlDbType.Uuid, "NpgsqlDbType.Uuid", 0)]);

        var broadcaster = new SpyRedisBroadcastFactoryWrapper();
        var service = new FldMasterService(cache, pg, NullLogger<CommonService>.Instance, broadcaster);

        var warm = await service.GetExecCommand(new TblFldExecutionCommand("hot_query"));
        Assert.True(warm.Status);

        using var cts = new CancellationTokenSource();
        var errors = new ConcurrentQueue<string>();
        int readsCompleted = 0;

        var readers = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                var r = await service.GetExecCommand(new TblFldExecutionCommand("hot_query"));
                if (!r.Status || r.Data == null)
                {
                    errors.Enqueue("Reader observed failed status or null CommandRModel");
                    continue;
                }

                var prms = r.Data.Parameters ?? [];
                bool isValidV1 = r.Data.CommandText == "SELECT v1 FROM t WHERE id = @id" && prms.Length == 1;
                bool isValidV2 = r.Data.CommandText == "SELECT v2 FROM t WHERE id = @id AND org = @org" && prms.Length == 2;
                if (!isValidV1 && !isValidV2)
                {
                    errors.Enqueue($"Observed torn query/params state: '{r.Data.CommandText}' with {prms.Length} params");
                }
                Interlocked.Increment(ref readsCompleted);
            }
        })).ToArray();

        await Task.Delay(30);
        pg.SeedQuery(
            tblId,
            "hot_query",
            "SELECT v2 FROM t WHERE id = @id AND org = @org",
            EExecType.Query,
            EDb.Postgres,
            [
                ("id", (int)NpgsqlDbType.Uuid, "NpgsqlDbType.Uuid", 0),
                ("org", (int)NpgsqlDbType.Varchar, "NpgsqlDbType.Varchar", 1)
            ]);

        var swap = await service.BulkSyncQueries(new BulkSyncQueriesCommand
        {
            DeployVersion = "v2-hot",
            Queries =
            [
                new TblMasterSyncItemCommand
                {
                    Id = tblId.ToString(),
                    Code = "hot_query",
                    Query = "SELECT v2 FROM t WHERE id = @id AND org = @org",
                    ExecType = (int)EExecType.Query,
                    DbType = (int)EDb.Postgres,
                    Fields =
                    [
                        new FldQueryMasterSyncCommand { FieldName = "id", FieldType = (int)NpgsqlDbType.Uuid, OrderSort = 0 },
                        new FldQueryMasterSyncCommand { FieldName = "org", FieldType = (int)NpgsqlDbType.Varchar, OrderSort = 1 }
                    ]
                }
            ]
        });

        await Task.Delay(30);
        cts.Cancel();
        await Task.WhenAll(readers);

        Assert.True(swap.Status);
        Assert.Empty(errors);
        Assert.True(readsCompleted > 50);
    }

    [Fact]
    public async Task Q04_FldMasterService_BulkSyncQueries_RollbackOnTxFail_PreservesCacheAndSkipsBroadcast()
    {
        EnsureCachingEnabledConfig();

        var cache = new WrapperCacheStore<string, List<TblFldRModel>>();
        cache.AddOrUpdate("code:stable_q",
        [
            new TblFldRModel { TblMaterCode = "stable_q", Query = "SELECT 1", ExecType = EExecType.Query, DataBaseType = EDb.Postgres }
        ], TimeSpan.FromMinutes(10));

        var pg = new FakeGeneralPostgresFactory { FailNextTransaction = true };
        var broadcaster = new SpyRedisBroadcastFactoryWrapper();
        var service = new FldMasterService(cache, pg, NullLogger<CommonService>.Instance, broadcaster);

        var res = await service.BulkSyncQueries(new BulkSyncQueriesCommand
        {
            Queries =
            [
                new TblMasterSyncItemCommand
                {
                    Code = "stable_q",
                    Query = "SELECT 2",
                    ExecType = (int)EExecType.Query,
                    DbType = (int)EDb.Postgres
                }
            ]
        });

        Assert.False(res.Status);
        Assert.Empty(broadcaster.PublishedMessages);
        Assert.True(cache.TryGetValue("code:stable_q", out var existing));
        Assert.Equal("SELECT 1", existing![0].Query);
    }

    [Fact]
    public async Task Q05_GeneralProtoCommandClient_L1Cache_ClonesExecuteCommand_PreventsConcurrentParamOverwrite()
    {
        var l1Cache = new GeneralCommandL1Cache();
        var fakeGrpc = new FakeFldMasterProtoClient();
        fakeGrpc.Specs["acc_by_id"] = new ProtoGeneral.CommandRModel
        {
            CommandText = "SELECT * FROM acc_srv_account WHERE id = @account_id",
            ExecType = (int)EExecType.Query,
            DatabaseType = (int)EDb.Postgres,
            Parameters =
            {
                new ProtoGeneral.NpOnDbCommandParamRModel { ParamName = "account_id", ParamType = (int)NpgsqlDbType.Varchar }
            }
        };

        var client = new GeneralProtoCommandClient(fakeGrpc, l1Cache);

        var commands = await Task.WhenAll(Enumerable.Range(0, 25).Select(async i =>
        {
            var cmd = await client.GetExecCommandAsync("acc_by_id");
            Assert.NotNull(cmd);
            cmd.AddParameterValue("account_id", $"user-{i}");
            return (Index: i, Command: cmd);
        }));

        // L1 cache hit after first gRPC fetch -> gRPC called once total.
        Assert.Equal(1, fakeGrpc.GetExecCommandCallCount);
        foreach (var (idx, cmd) in commands)
        {
            Assert.Equal($"user-{idx}", cmd.Parameters![0].ParamValue);
        }
    }

    [Fact]
    public async Task Q06_GeneralProtoCommandClient_RefreshCodesAsync_FetchThenSwap_PreservesOldOnRpcError()
    {
        var l1Cache = new GeneralCommandL1Cache();
        var fakeGrpc = new FakeFldMasterProtoClient();
        fakeGrpc.Specs["q_live"] = new ProtoGeneral.CommandRModel
        {
            CommandText = "SELECT 'v1'",
            ExecType = (int)EExecType.Query,
            DatabaseType = (int)EDb.Postgres
        };

        var client = new GeneralProtoCommandClient(fakeGrpc, l1Cache);
        var initial = await client.GetExecCommandAsync("q_live");
        Assert.Equal("SELECT 'v1'", initial!.CommandText);

        // Simulate GeneralService transient failure during refresh -> old cached entry stays intact.
        fakeGrpc.ThrowOnCall = true;
        int failedRefresh = await client.RefreshCodesAsync(["q_live"]);
        Assert.Equal(0, failedRefresh);
        var stillValid = await client.GetExecCommandAsync("q_live");
        Assert.Equal("SELECT 'v1'", stillValid!.CommandText);

        // GeneralService back up with v2 -> atomic fetch-then-swap updates L1 cleanly.
        fakeGrpc.ThrowOnCall = false;
        fakeGrpc.Specs["q_live"] = new ProtoGeneral.CommandRModel
        {
            CommandText = "SELECT 'v2'",
            ExecType = (int)EExecType.Query,
            DatabaseType = (int)EDb.Postgres
        };
        int refreshed = await client.RefreshCodesAsync(["q_live"]);
        Assert.Equal(1, refreshed);
        var updated = await client.GetExecCommandAsync("q_live");
        Assert.Equal("SELECT 'v2'", updated!.CommandText);
    }

    [Fact]
    public async Task Q07_AccountPermissionStorageAdapter_DeleteOldVersion_FallsBackToTypedUuidSql()
    {
        var pg = new RecordingPostgresFactory();
        var cmdClient = new FakeGeneralCommandClient(returnNull: true);
        var adapter = new AccountPermissionStorageAdapter(pg, cmdClient);

        string versionId = IndexerMode.CreateGuid().ToString();
        bool ok = await adapter.AccountPermissionExceptionDeleteOldVersionByHostCode("NpOn.SSO", versionId);

        Assert.True(ok);
        Assert.Contains("DELETE FROM acc_srv_account_permission_controller", pg.LastSql);
        var versionParam = Assert.Single(pg.LastParameters, p => p.ParamName == "version_id");
        Assert.IsType<Guid>(versionParam.ParamValue);
    }

    [Fact]
    public async Task R01_RedisBroadcastFactoryWrapper_PublisherOnlyMode_DoesNotFailCommandNotSupported()
    {
        var option = new RedisConnectOption().SetConnectionString("127.0.0.1:59998,abortConnect=false,connectTimeout=150");
        using var wrapper = new RedisBroadcastFactoryWrapper(option);

        bool built = wrapper.BuildFactory(out string? err);
        Assert.True(built);
        Assert.Null(err);

        var res = await wrapper.PublishAsync(new RedisBroadcastMessage<GeneralQuerySyncBroadcastMessage>
        {
            Channel = GeneralQuerySyncBroadcastConstant.GeneralQuerySyncChannel,
            Value = new GeneralQuerySyncBroadcastMessage { Codes = ["q1"], DeployVersion = "d1" }
        });

        Assert.NotNull(res);
        // Offline broker returns EDbError.Connection (NOT CommandNotSupported from missing local handler).
        Assert.False(res.Status);
    }

    [Fact]
    public async Task R02_RedisDriver_OfflineBroker_FailsWithConnectionErrorCleanly()
    {
        var option = new RedisConnectOption().SetConnectionString("127.0.0.1:59997,abortConnect=false,connectTimeout=150");
        await using var driver = new RedisDriver(option);

        var res = await driver.Execute(new Common.Infrastructures.NpOn.RedisExtCm.Commands.RedisDbCommand(
            "k1", ERedisCommand.Get));

        Assert.False(res.Status);
        await driver.DisconnectAsync();
    }

    [Fact]
    public async Task R03_RabbitMqConnectionAndProducer_BrokenBroker_DoesNotCrashOrLeakUnobservedTask()
    {
        using var conn = new RabbitMqConnection("amqp://guest:guest@127.0.0.1:59996/", "npon.test.ex");
        Assert.False(conn.IsConnected);

        bool connected = await conn.EnsureConnectedAsync();
        Assert.False(connected);

        var producer = new RabbitMqProducer(conn, NullLogger<RabbitMqProducer>.Instance);
        var evt = new RabbitMqEvent<TestRabbitContent>
        {
            MessageContent = new TestRabbitContent { Payload = "resilience-check" }
        };

        var unobserved = 0;
        EventHandler<UnobservedTaskExceptionEventArgs> handler = (_, args) =>
        {
            Interlocked.Increment(ref unobserved);
            args.SetObserved();
        };
        TaskScheduler.UnobservedTaskException += handler;
        try
        {
            producer.AddEvent(evt);
            await Task.Delay(350);
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= handler;
        }

        Assert.Equal(0, unobserved);
    }

    [Fact]
    public async Task R04_BaseDbFactoryWrapper_TransientRetryAndFailFast_ReleasesAllConnections()
    {
        var driver = new TransientTestDriver();
        var factory = new SingleDriverTestFactory(driver);
        var wrapper = new TestDbFactoryWrapper(factory);

        // Attempt 1 throws TimeoutException with !IsValidSession -> heals on attempt 2 and succeeds.
        driver.Mode = TransientTestDriver.FailMode.TimeoutThenSucceed;
        var res = await wrapper.ExecuteAsync("SELECT 1", []);
        Assert.NotNull(res);
        Assert.True(res.Status);
        Assert.Equal(2, driver.Attempts);
        Assert.Equal(factory.AcquireCount, factory.ReleaseCount);

        // Non-transient InvalidOperationException with healthy session fails fast on attempt 1.
        driver.Reset();
        driver.Mode = TransientTestDriver.FailMode.HealthySessionError;
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrapper.ExecuteAsync("SELECT 1", []));
        Assert.Equal(1, driver.Attempts);
        Assert.Equal(factory.AcquireCount, factory.ReleaseCount);
    }

    [Fact]
    public void Q08_FldMasterProtoMapper_ToProtoCommandSpec_PreservesTypedEnumParamType()
    {
        var payload = ProtoBufMode.ProtoBufSerialize(new NpOnDbCommandParamGrpcList
        {
            Items =
            [
                new NpOnDbCommandParamGrpc
                {
                    ParamName = "id",
                    ParamType = (int)NpgsqlDbType.Uuid
                },
                new NpOnDbCommandParamGrpc
                {
                    ParamName = "status",
                    ParamType = (int)NpgsqlDbType.Integer
                }
            ]
        });

        var source = new CommandRModel
        {
            CommandText = "SELECT * FROM acc_srv_account WHERE id = @id AND status = @status",
            ParamsPayload = payload,
            ExecType = EExecType.Query,
            DatabaseType = EDb.Postgres,
            DeserializeParamType = typeof(NpgsqlDbType)
        };

        // Also ensure NpOnDbCommandParam<TEnum> keeps base.ParamType synchronized directly.
        var directTypedParam = new NpOnDbCommandParam<NpgsqlDbType>("id", Guid.Empty, NpgsqlDbType.Uuid);
        Assert.Equal(NpgsqlDbType.Uuid, ((NpOnDbCommandParam)directTypedParam).ParamType);

        var protoSpec = FldMasterProtoMapper.ToProtoCommandSpec(source);
        Assert.Equal(2, protoSpec.Parameters.Count);
        Assert.True(protoSpec.Parameters[0].HasParamType);
        Assert.Equal((int)NpgsqlDbType.Uuid, protoSpec.Parameters[0].ParamType);
        Assert.True(protoSpec.Parameters[1].HasParamType);
        Assert.Equal((int)NpgsqlDbType.Integer, protoSpec.Parameters[1].ParamType);

        var cloned = GeneralProtoCommandClient.CloneExecuteCommand(protoSpec);
        Assert.NotNull(cloned.Parameters);
        var clonedIdParam = Assert.IsAssignableFrom<INpOnDbCommandParam<NpgsqlDbType>>(cloned.Parameters[0]);
        Assert.Equal(NpgsqlDbType.Uuid, clonedIdParam.ParamType);
    }

    [Fact]
    public async Task R05_RedisFactoryWrapper_And_PermissionRepo_OfflineBroker_FailsFastWithoutHanging()
    {
        var option = new RedisConnectOption().SetConnectionString("127.0.0.1:59995,abortConnect=true,connectTimeout=150");
        var redisWrapper = new RedisFactoryWrapper(option, connectionNumber: 5, isUseCaching: false);
        var repo = new AccountTokenAndPermissionRedisRepository(redisWrapper);

        var swFirst = Stopwatch.StartNew();
        long version = await repo.GetPermissionCatalogVersion();
        var state = await repo.GetAccountPermissionState(Guid.NewGuid().ToString());
        bool written = await repo.SetAccountPermissionState(Guid.NewGuid().ToString(), new MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels.AccountPermissionExceptionCacheState());
        swFirst.Stop();

        Assert.Equal(0, version);
        Assert.Null(state);
        Assert.False(written);
        // Must fail fast (< 2s) instead of blocking 30s in BaseDbFactoryWrapper.AcquireConnectionAsync.
        Assert.True(swFirst.ElapsedMilliseconds < 2000, $"Expected fast fail (<2000ms), got {swFirst.ElapsedMilliseconds}ms");

        // Subsequent calls during circuit cooldown should short-circuit near-instantly (< 50ms).
        var swCooldown = Stopwatch.StartNew();
        Assert.Equal(0, await repo.GetPermissionCatalogVersion());
        Assert.Null(await repo.GetAccountPermissionState(Guid.NewGuid().ToString()));
        swCooldown.Stop();
        Assert.True(swCooldown.ElapsedMilliseconds < 50, $"Expected circuit-open short-circuit (<50ms), got {swCooldown.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void Q09_PostgresUtils_ConvertStringToNpgsqlType_SupportsTypedArrays()
    {
        object? strArrObj = PostgresUtils.ConvertStringToNpgsqlType(
            "NpOn.SSO.Account.ChangePassword, NpOn.SSO.Account.Signup",
            NpgsqlDbType.Array | NpgsqlDbType.Varchar);
        string[] strArr = Assert.IsType<string[]>(strArrObj);
        Assert.Equal(["NpOn.SSO.Account.ChangePassword", "NpOn.SSO.Account.Signup"], strArr);

        Guid g1 = Guid.NewGuid();
        Guid g2 = Guid.NewGuid();
        object? guidArrObj = PostgresUtils.ConvertStringToNpgsqlType(
            $"{g1},{g2}",
            NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        Guid[] guidArr = Assert.IsType<Guid[]>(guidArrObj);
        Assert.Equal([g1, g2], guidArr);

        object? emptyGuidArrObj = PostgresUtils.ConvertStringToNpgsqlType(
            string.Empty,
            NpgsqlDbType.Array | NpgsqlDbType.Uuid);
        Guid[] emptyGuidArr = Assert.IsType<Guid[]>(emptyGuidArrObj);
        Assert.Empty(emptyGuidArr);

        object? intArrObj = PostgresUtils.ConvertStringToNpgsqlType(
            "2, 64",
            NpgsqlDbType.Array | NpgsqlDbType.Integer);
        int[] intArr = Assert.IsType<int[]>(intArrObj);
        Assert.Equal([2, 64], intArr);
    }

    [Fact]
    public async Task Q10_AuthenticationService_FailClosed_WhenControllerLookupFailsOrMissing()
    {
        Guid accountId = Guid.NewGuid();
        var authAdapter = new FailClosedTestAuthAdapter(new AccountRModel
        {
            Id = accountId,
            UserName = "downgraded_user",
            Password = "hashed-password",
            Permission = EPermission.User,
            Status = EAccountStatus.Active
        });
        var redisRepo = new InMemoryPermRedisRepo();
        var permAdapter = new FailClosedTestPermAdapter();
        var permService = new AccountPermissionService(
            authAdapter,
            permAdapter,
            redisRepo,
            new DummyGroupStorageAdapter(),
            new RecordingPostgresFactory(),
            NullLogger<AccountPermissionService>.Instance);

        var authService = new AuthenticationService(
            new RecordingPostgresFactory(),
            authAdapter,
            new NoOpZeroMqFactory(),
            permService,
            redisRepo,
            new NoOpRabbitProducer(),
            NullLogger<CommonService>.Instance);

        // Case 1: Controller DB lookup returns null (transient error / array bind failure)
        permAdapter.ControllersResult = null;
        var resWhenError = await authService.IsValidLogonPermissionExceptionControllers(
            new AccountPermissionExceptionCachingCheckValidQuery
            {
                AccountId = accountId.ToString(),
                ControllerCode = "NpOn.SSO.Account.ChangePassword",
                IsHasBasePermission = true // Stale JWT claim!
            });
        Assert.True(resWhenError.Status);
        Assert.False(resWhenError.Data);

        // Case 2: Controller lookup succeeds with empty list (deleted / missing controller)
        permAdapter.ControllersResult = [];
        var resWhenMissing = await authService.IsValidLogonPermissionExceptionControllers(
            new AccountPermissionExceptionCachingCheckValidQuery
            {
                AccountId = accountId.ToString(),
                ControllerCode = "NpOn.SSO.Account.DeletedEndpoint",
                IsHasBasePermission = true // Stale JWT claim!
            });
        Assert.True(resWhenMissing.Status);
        Assert.False(resWhenMissing.Data);
    }

    [Fact]
    public async Task R06_SharedGrpcInterceptor_ConcurrentCalls_IsolatesPerCallMetadata()
    {
        var headerConfig = new GrpcHeaderConfig(EGrpcEndUseType.InternalServer, headers: null);
        var accessor = new HttpContextAccessor();
        var interceptor = new SharedGrpcInterceptor(NullLogger<GrpcInterceptorBase>.Instance, headerConfig, accessor);
        var method = new Method<string, string>(MethodType.Unary, "TestSvc", "Call",
            Marshallers.StringMarshaller, Marshallers.StringMarshaller);

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(async () =>
        {
            var httpCtx = new DefaultHttpContext();
            bool hasAuth = i % 2 == 0;
            if (hasAuth)
            {
                httpCtx.Request.Headers.Authorization = $"Bearer tok-{i}";
                httpCtx.Request.Headers[DefaultHeaderConstant.GrpcInternalCallerSessionCode] = $"sess-{i}";
            }

            accessor.HttpContext = httpCtx;
            await Task.Yield();

            Metadata? capturedHeaders = null;
            var ctx = new ClientInterceptorContext<string, string>(method, "localhost", new CallOptions());
            var call = interceptor.AsyncUnaryCall("req", ctx, (_, interceptedCtx) =>
            {
                capturedHeaders = interceptedCtx.Options.Headers;
                return new AsyncUnaryCall<string>(
                    Task.FromResult("ok"),
                    Task.FromResult(new Metadata()),
                    () => Status.DefaultSuccess,
                    () => [],
                    () => { });
            });

            await call.ResponseAsync;
            Assert.NotNull(capturedHeaders);
            if (hasAuth)
            {
                Assert.Equal($"Bearer tok-{i}", capturedHeaders!.GetValue("authorization"));
                Assert.Equal($"sess-{i}", capturedHeaders.GetValue(DefaultHeaderConstant.GrpcInternalCallerSessionCode));
            }
            else
            {
                Assert.Null(capturedHeaders!.Get("authorization"));
                Assert.Equal(string.Empty, capturedHeaders.GetValue(DefaultHeaderConstant.GrpcInternalCallerSessionCode));
            }
        }));

        await Task.WhenAll(tasks);

        // Baseline shared Metadata must remain uncontaminated by per-request Authorization headers.
        Assert.Null(headerConfig.GetHeader()?.Get("authorization"));
    }

    [Fact]
    public async Task R07_RabbitMqConsumer_AutoResubscribes_WhenConnectionReconnectedAsyncFires()
    {
        var channel1Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel1State = (FakeRabbitChannelProxy)(object)channel1Proxy;
        var channel2Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel2State = (FakeRabbitChannelProxy)(object)channel2Proxy;

        var conn = new FakeReconnectingRabbitConnection(channel1Proxy);
        using var consumer = new TestRabbitConsumer(conn);

        Assert.Equal(1, channel1State.ConsumeCallCount);
        Assert.Equal(0, channel2State.ConsumeCallCount);

        // Calling UseDefault on same open channel must remain idempotent.
        await consumer.UseDefault();
        Assert.Equal(1, channel1State.ConsumeCallCount);

        // Simulate RabbitMQ restart where RabbitMqConnection replaces Channel with channel2 and emits ReconnectedAsync.
        await conn.SimulateReconnectAsync(channel2Proxy);
        Assert.Equal(1, channel2State.ConsumeCallCount);
    }

    [Fact]
    public void Q11_AccountDomain_FromAccountRModel_PreservesActiveStatus()
    {
        var accountId = Guid.NewGuid();
        var model = new AccountRModel
        {
            Id = accountId,
            UserName = "admin_user",
            Password = "old-hash",
            Permission = EPermission.Administrator,
            Status = EAccountStatus.Active
        };

        var domain = new MicroServices.Account.Contracts.NpOn.AccountServiceDomain.Domains.Account(model);
        Assert.Equal(EAccountStatus.Active, domain.Status);

        domain.ChangeNewPassword(new AccountChangePasswordCommand
        {
            AccountId = accountId,
            NewPassword = "new-hash"
        });
        Assert.Equal(EAccountStatus.Active, domain.Status);
        Assert.Equal("new-hash", domain.Password);
    }

    [Fact]
    public async Task R08_RabbitMqConsumer_UseDefault_InlineReconnectedEvent_DoesNotDeadlock()
    {
        var channel1Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel1State = (FakeRabbitChannelProxy)(object)channel1Proxy;
        var channel2Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel2State = (FakeRabbitChannelProxy)(object)channel2Proxy;

        var conn = new FakeReconnectingRabbitConnection(channel1Proxy);
        using var consumer = new TestRabbitConsumer(conn);
        Assert.Equal(1, channel1State.ConsumeCallCount);

        // Simulate EnsureConnectedAsync() synchronously firing ReconnectedAsync while UseDefault() runs.
        conn.PendingReconnectChannelInEnsure = channel2Proxy;
        var t1 = consumer.UseDefault();
        var completed = await Task.WhenAny(t1, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(t1, completed);
        await t1;
        Assert.Equal(1, channel2State.ConsumeCallCount);

        // Also test inline ReconnectedAsync inside AddDefaultQueue while _consumeLock IS held.
        var channel3Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel3State = (FakeRabbitChannelProxy)(object)channel3Proxy;
        conn.PendingReconnectChannelInAddDefaultQueue = channel3Proxy;
        var t2 = consumer.UseDefault();
        var done2 = await Task.WhenAny(t2, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(t2, done2);
        await t2;
        Assert.Equal(1, channel3State.ConsumeCallCount);
    }

    [Fact]
    public async Task R09_RabbitMqConsumer_ReconnectsCleanly_WhenPreviousChannelDisposedOnUnsubscribe()
    {
        var channel1Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel1State = (FakeRabbitChannelProxy)(object)channel1Proxy;
        var channel2Proxy = DispatchProxy.Create<IChannel, FakeRabbitChannelProxy>();
        var channel2State = (FakeRabbitChannelProxy)(object)channel2Proxy;

        var conn = new FakeReconnectingRabbitConnection(channel1Proxy);
        using var consumer = new TestRabbitConsumer(conn);
        Assert.Equal(1, channel1State.ConsumeCallCount);

        // Simulate RabbitMqConnection.CreateConnection() having already disposed channel1 before UseDefault unsubscribes.
        channel1State.ThrowObjectDisposedOnUnsubscribe = true;
        channel1State.Open = false;
        await conn.SimulateReconnectAsync(channel2Proxy);

        Assert.Equal(1, channel2State.ConsumeCallCount);
    }

    #region Test Doubles

    [ProtoBuf.ProtoContract]
    private sealed class TestRabbitContent : CommonMessageContent
    {
        [ProtoBuf.ProtoMember(1)] public string Payload { get; set; } = string.Empty;
    }

    private sealed class SpyRedisBroadcastFactoryWrapper : IRedisBroadcastFactoryWrapper
    {
        public int HandlerCount => 0;
        public IDbDriverFactory? Factory { get; set; }
        public EDb DbType { get; set; } = EDb.Redis;
        public List<BaseBroadcastMessage> PublishedMessages { get; } = [];

        public bool BuildFactory(out string? errorString)
        {
            errorString = null;
            return true;
        }

        public void DestroyInternal() { }

        public Task<INpOnWrapperResult?> PublishAsync(BaseBroadcastMessage message)
        {
            PublishedMessages.Add(message);
            return Task.FromResult<INpOnWrapperResult?>(new RedisValueWrapper(new RedisValueContainer((RedisValue)3L)));
        }

        public void Dispose() { }
    }

    private sealed class FakeGeneralCommandClient(bool returnNull) : IGeneralCommandClient
    {
        public Task<NpOnDbExecuteCommand?> GetExecCommandAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<NpOnDbExecuteCommand?>(returnNull ? null : new NpOnDbExecuteCommand
            {
                CommandText = "SELECT 1",
                ExecType = EExecType.Query,
                Parameters = []
            });

        public Task<int> RefreshCodesAsync(string[]? codes, bool isFullReload = false, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }

    private sealed class RecordingPostgresFactory : IPostgresFactoryWrapper
    {
        public string? LastSql { get; private set; }
        public List<INpOnDbCommandParam> LastParameters { get; private set; } = [];
        public string? FactoryOptionCode => "recording-pg";
        public EDb GetDbType() => EDb.Postgres;

        public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters)
        {
            LastSql = queryString;
            LastParameters = parameters;
            var res = new NpOnWrapperResult();
            res.SetSuccess();
            return Task.FromResult<INpOnWrapperResult?>(res);
        }

        public Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnExecuteCommand) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> BulkCopy(INpOnTableWrapper table, string tableName) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand) => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand dbCommand) where T : class => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(string queryString, List<INpOnDbCommandParam>? parameters = null) where T : class => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName, List<INpOnDbCommandParam>? parameters) => throw new NotImplementedException();
        public Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(IEnumerable<IBaseNpOnDbCommand> commands) => throw new NotImplementedException();
        public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
    }

    private sealed class FakeGeneralPostgresFactory : IPostgresFactoryWrapper
    {
        private readonly Dictionary<string, List<TblFldRModel>> _store = new(StringComparer.Ordinal);
        public int SelectCount;
        public int TransactionCount;
        public bool FailNextTransaction { get; set; }

        public string? FactoryOptionCode => "fake-general-pg";
        public EDb GetDbType() => EDb.Postgres;

        public void SeedQuery(
            Guid tblId,
            string code,
            string query,
            EExecType execType,
            EDb dbType,
            (string Name, int? Type, string? TypeString, int Sort)[] fields)
        {
            var list = new List<TblFldRModel>();
            if (fields.Length == 0)
            {
                list.Add(new TblFldRModel
                {
                    TblMaterId = tblId,
                    TblMaterCode = code,
                    Query = query,
                    ExecType = execType,
                    DataBaseType = dbType,
                });
            }
            else
            {
                foreach (var f in fields)
                {
                    list.Add(new TblFldRModel
                    {
                        TblMaterId = tblId,
                        TblMaterCode = code,
                        Query = query,
                        ExecType = execType,
                        DataBaseType = dbType,
                        FldMasterId = Guid.NewGuid(),
                        FieldName = f.Name,
                        FieldType = f.Type.HasValue ? (NpgsqlDbType)f.Type.Value : null,
                        FieldTypeString = f.TypeString,
                        OrderSort = f.Sort
                    });
                }
            }
            _store[code] = list;
        }

        public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters)
        {
            Interlocked.Increment(ref SelectCount);
            IEnumerable<TblFldRModel> matched = [];
            var codeParam = parameters.FirstOrDefault(p => p.ParamName == "Code")?.ParamValue?.ToString();
            var codesCsv = parameters.FirstOrDefault(p => p.ParamName == "codes")?.ParamValue?.ToString();
            if (!string.IsNullOrEmpty(codeParam) && _store.TryGetValue(codeParam, out var single))
            {
                matched = single;
            }
            else if (!string.IsNullOrEmpty(codesCsv))
            {
                var set = codesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                matched = set.Where(c => _store.ContainsKey(c)).SelectMany(c => _store[c]);
            }

            var rows = matched.Select(m => (INpOnRowWrapper)new MockRowWrapper(new Dictionary<string, INpOnCell>
            {
                ["tbl_id"] = new NpOnCell<Guid?>(m.TblMaterId, DbType.Guid, "uuid"),
                ["tbl_code"] = new NpOnCell<string?>(m.TblMaterCode, DbType.String, "varchar"),
                ["query"] = new NpOnCell<string?>(m.Query, DbType.String, "text"),
                ["exec_type"] = new NpOnCell<int?>((int?)m.ExecType, DbType.Int32, "int4"),
                ["db_type"] = new NpOnCell<int?>((int?)m.DataBaseType, DbType.Int32, "int4"),
                ["fld_id"] = new NpOnCell<Guid?>(m.FldMasterId, DbType.Guid, "uuid"),
                ["order_sort"] = new NpOnCell<int?>(m.OrderSort, DbType.Int32, "int4"),
                ["field_name"] = new NpOnCell<string?>(m.FieldName, DbType.String, "varchar"),
                ["field_type"] = new NpOnCell<int?>((int?)m.FieldType, DbType.Int32, "int4"),
                ["field_type_string"] = new NpOnCell<string?>(m.FieldTypeString, DbType.String, "varchar")
            })).ToList();

            var wrapper = new MockTableWrapper(rows);
            return Task.FromResult<INpOnWrapperResult?>(wrapper);
        }

        public Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(IEnumerable<IBaseNpOnDbCommand> commands)
        {
            Interlocked.Increment(ref TransactionCount);
            var dict = new Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>();
            foreach (var cmd in commands)
            {
                var r = new NpOnWrapperResult();
                if (FailNextTransaction) r.SetFail(EDbError.Command);
                else r.SetSuccess();
                dict[cmd] = r;
            }
            return Task.FromResult<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?>(dict);
        }

        public Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnExecuteCommand) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> BulkCopy(INpOnTableWrapper table, string tableName) => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand) => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand dbCommand) where T : class => throw new NotImplementedException();
        public Task<List<T>> QueryAsync<T>(string queryString, List<INpOnDbCommandParam>? parameters = null) where T : class => throw new NotImplementedException();
        public Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName, List<INpOnDbCommandParam>? parameters) => throw new NotImplementedException();
        public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain => throw new NotImplementedException();
    }

    private sealed class FakeFldMasterProtoClient : ProtoGeneral.FldMasterProtoService.FldMasterProtoServiceClient
    {
        public Dictionary<string, ProtoGeneral.CommandRModel> Specs { get; } = new(StringComparer.Ordinal);
        public int GetExecCommandCallCount;
        public bool ThrowOnCall { get; set; }

        public override AsyncUnaryCall<ProtoGeneral.CommandRModelResponseProto> GetExecCommandAsync(
            ProtoGeneral.TblFldExecutionCommand request,
            Metadata? headers = null,
            DateTime? deadline = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref GetExecCommandCallCount);
            if (ThrowOnCall)
            {
                return new AsyncUnaryCall<ProtoGeneral.CommandRModelResponseProto>(
                    Task.FromException<ProtoGeneral.CommandRModelResponseProto>(new RpcException(new Status(StatusCode.Unavailable, "Simulated outage"))),
                    Task.FromResult(new Metadata()), () => Status.DefaultCancelled, () => [], () => { });
            }

            var reply = Specs.TryGetValue(request.Code, out var spec)
                ? new ProtoGeneral.CommandRModelResponseProto { Status = true, Data = spec }
                : new ProtoGeneral.CommandRModelResponseProto { Status = false };

            return new AsyncUnaryCall<ProtoGeneral.CommandRModelResponseProto>(
                Task.FromResult(reply), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }
    }

    private sealed class TransientTestDriver : INpOnDbDriver
    {
        public enum FailMode { TimeoutThenSucceed, HealthySessionError }
        public FailMode Mode { get; set; }
        public int Attempts { get; private set; }
        public string Name => "TransientTestDriver";
        public string Version => "1.0";
        public bool IsValidSession { get; private set; } = true;
        public INpOnConnectOption Option { get; } = new PostgresConnectOption().SetConnectionString("Host=localhost;Database=test");
        public bool IsBusy { get; set; }
        public void ResetSessionTimeout() { }
        public void Reset() { Attempts = 0; IsValidSession = true; }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            IsValidSession = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync() => Task.CompletedTask;

        public Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
        {
            Attempts++;
            if (Mode == FailMode.TimeoutThenSucceed && Attempts == 1)
            {
                IsValidSession = false;
                throw new TimeoutException("Simulated connection drop on attempt 1");
            }

            if (Mode == FailMode.HealthySessionError)
            {
                IsValidSession = true;
                throw new InvalidOperationException("Non-transient application query error");
            }

            var ok = new NpOnWrapperResult();
            ok.SetSuccess();
            return Task.FromResult<INpOnWrapperResult>(ok);
        }

        public Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand? command) where T : class => Task.FromResult<List<T>>([]);
        public Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(IEnumerable<IBaseNpOnDbCommand> commands, CancellationToken cancellationToken = default)
            => Task.FromResult(new Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>());
        public Task<bool> IsAliveAsync(CancellationToken cancellationToken = default) => Task.FromResult(IsValidSession);
    }

    private sealed class TestDbConnection(INpOnDbDriver driver) : NpOnDbConnection
    {
        public override INpOnDbDriver Driver => driver;
    }

    private sealed class SingleDriverTestFactory(TransientTestDriver driver) : IDbDriverFactory
    {
        private readonly NpOnDbConnection _conn = new TestDbConnection(driver);
        public int AcquireCount;
        public int ReleaseCount;

        public int GetAliveConnectionNumbers => 1;
        public long ConnectionTimeoutSessions() => 5;
        public int GetConnectionNumbers => 1;
        public EDb GetDbType() => EDb.Postgres;
        public List<NpOnDbConnection>? ValidConnections => [_conn];
        public NpOnDbConnection? FirstValidConnection => _conn;
        public string DriverOptionKey => "test-driver";

        public ValueTask<NpOnDbConnection?> GetConnectionAsync()
        {
            Interlocked.Increment(ref AcquireCount);
            return ValueTask.FromResult<NpOnDbConnection?>(_conn);
        }

        public void ReleaseConnection(NpOnDbConnection connection) => Interlocked.Increment(ref ReleaseCount);
        public IDbDriverFactory WithDatabaseType(EDb eDb) => this;
        public IDbDriverFactory WithOption(INpOnConnectOption option) => this;
        public IDbDriverFactory CreateConnections(int connectionNumber = 1) => this;
        public Task<IDbDriverFactory> Reset(bool isResetParameters = false) => Task.FromResult<IDbDriverFactory>(this);
        public Task<int> OpenConnections(int connectionNumber = 1, bool isAutoFixConnectionNumber = true, bool isUseException = false) => Task.FromResult(1);
    }

    private sealed class TestDbFactoryWrapper(IDbDriverFactory factory) : BaseDbFactoryWrapper(factory, isUseCaching: false)
    {
    }

    private sealed class FailClosedTestAuthAdapter(AccountRModel account) : IAuthenticationStorageAdapter
    {
        public Task<List<AccountRModel>?> AccountGetByNumberPhoneOrEmailOrUsername(string phoneNumber, string email, string username) => Task.FromResult<List<AccountRModel>?>([]);
        public Task<AccountRModel?> AccountGetByUsernameAndPassword(string username, string password) => Task.FromResult<AccountRModel?>(null);
        public Task<List<AccountLoginRModel>?> AccountLoginInfoGetByRefreshToken(string refreshToken) => Task.FromResult<List<AccountLoginRModel>?>([]);
        public Task<AccountRModel?> AccountGetById(string accountId) => Task.FromResult<AccountRModel?>(account);
        public Task<List<AccountRModel>?> AccountGetByIds(string[]? accountIds) => Task.FromResult<List<AccountRModel>?>([account]);
        public Task<AccountLoginRModel?> AccountLoginInfoGetBySessionId(string sessionId) => Task.FromResult<AccountLoginRModel?>(null);
    }

    private sealed class FailClosedTestPermAdapter : IAccountPermissionStorageAdapter
    {
        public List<AccountPermissionControllerRModel>? ControllersResult { get; set; }
        public Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountId(string accountId) => Task.FromResult<List<AccountPermissionExceptionRModel>?>([]);
        public Task<bool> AccountPermissionExceptionDeleteOldVersionByHostCode(string hostCode, string versionId) => Task.FromResult(true);
        public Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountIdAndControllerCodes(string accountId, string[] controllerCodes) => Task.FromResult<List<AccountPermissionExceptionRModel>?>([]);
        public Task<List<AccountPermissionControllerRModel>?> AccountPermissionControllerGetByCodes(string[]? codes) => Task.FromResult(ControllersResult);
        public Task<bool> AccountPermissionExceptionDeleteOrphaned() => Task.FromResult(true);
        public Task<bool> AccountPermissionExceptionsReconcileByControllerCodes(string[] controllerCodes) => Task.FromResult(true);
    }

    private sealed class InMemoryPermRedisRepo : IAccountTokenAndPermissionRedisRepository
    {
        private AccountPermissionExceptionCacheState? _state;
        public Task<bool> AddCachingPermissionException(string accountId, AccountPermissionExceptionRModel[]? accountPermissions) => Task.FromResult(true);
        public Task<string[]?> GetCachingPermissionException(string accountId) => Task.FromResult<string[]?>([]);
        public Task<bool> SetAccountPermissionState(string accountId, AccountPermissionExceptionCacheState state) { _state = state; return Task.FromResult(true); }
        public Task<AccountPermissionExceptionCacheState?> GetAccountPermissionState(string accountId) => Task.FromResult(_state);
        public Task<long> GetPermissionCatalogVersion() => Task.FromResult(1L);
        public Task<long> BumpPermissionCatalogVersion() => Task.FromResult(2L);
        public Task<bool> DeleteCachingPermissionExceptionsByAccountId(string accountId) { _state = null; return Task.FromResult(true); }
        public Task<bool> DeleteCachingPermissionExceptionsByAccountIds(string[] accountIds) { _state = null; return Task.FromResult(true); }
    }

    private sealed class DummyGroupStorageAdapter : IAccountGroupStorageAdapter
    {
        public Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(string[] groupIds, int pageSize, int pageIndex) => Task.FromResult<List<AccountGroupRModel>?>([]);
    }

    private sealed class NoOpRabbitProducer : IRabbitMqProducer
    {
        public void AddEvent(IRabbitMqEvent @event, bool isCompress = false) { }
    }

    private sealed class NoOpZeroMqFactory : IZeroMqTwoWayFactory
    {
        public Task<IZeroMqTwoWayProvider> CreateClientAsync(EUrlConfiguration urlConfig) => throw new NotImplementedException();
        public IZeroMqTwoWayProvider? TryGet(EUrlConfiguration urlConfiguration) => null;
        public Task<bool> TrySendTo<TRequest>(IEnumerable<EUrlConfiguration>? configurations, TRequest request) => Task.FromResult(true);
    }

    public class FakeRabbitChannelProxy : DispatchProxy
    {
        public int ConsumeCallCount { get; private set; }
        public bool Open { get; set; } = true;
        public bool ThrowObjectDisposedOnUnsubscribe { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null) return null;
            if (targetMethod.Name == "remove_ChannelShutdownAsync" && ThrowObjectDisposedOnUnsubscribe)
                throw new ObjectDisposedException("AutorecoveringChannel");

            return targetMethod.Name switch
            {
                "get_IsOpen" => Open,
                "BasicQosAsync" => Task.CompletedTask,
                "BasicConsumeAsync" => IncrementAndReturnTag(),
                "add_ChannelShutdownAsync" or "remove_ChannelShutdownAsync" => null,
                _ => targetMethod.ReturnType == typeof(Task)
                    ? Task.CompletedTask
                    : targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null
            };
        }

        private Task<string> IncrementAndReturnTag()
        {
            ConsumeCallCount++;
            return Task.FromResult($"tag-{ConsumeCallCount}");
        }
    }

    private sealed class FakeReconnectingRabbitConnection(IChannel initialChannel) : IRabbitMqConnection
    {
        private IChannel _channel = initialChannel;
        public IChannel Channel => _channel;
        public string RoutingKey => "test.rk";
        public string ExchangeName => "test.ex";
        public bool IsConnected => _channel.IsOpen;
        public event Func<Task>? ReconnectedAsync;
        public IChannel? PendingReconnectChannelInEnsure { get; set; }
        public IChannel? PendingReconnectChannelInAddDefaultQueue { get; set; }

        public async Task<bool> EnsureConnectedAsync()
        {
            if (PendingReconnectChannelInEnsure != null)
            {
                var next = PendingReconnectChannelInEnsure;
                PendingReconnectChannelInEnsure = null;
                await SimulateReconnectAsync(next);
            }

            return IsConnected;
        }

        public async Task<string> AddDefaultQueue(string exchangeName, string queueName,
            bool isCreateNewExchangeWhenExisted = false, bool isCreateNewQueueWhenExisted = false,
            string? topicRoutingKey = null, ERabbitMqExchangeType exchangeType = ERabbitMqExchangeType.Direct)
        {
            if (PendingReconnectChannelInAddDefaultQueue != null)
            {
                var next = PendingReconnectChannelInAddDefaultQueue;
                PendingReconnectChannelInAddDefaultQueue = null;
                await SimulateReconnectAsync(next);
            }

            return queueName;
        }

        public Task AddQueue(RabbitMqQueueProperty property, bool isCreateNewExchangeWhenExisted = false,
            bool isCreateNewQueueWhenExisted = false) => Task.CompletedTask;

        public async Task SimulateReconnectAsync(IChannel nextChannel)
        {
            _channel = nextChannel;
            if (ReconnectedAsync != null)
            {
                foreach (Func<Task> h in ReconnectedAsync.GetInvocationList())
                    await h();
            }
        }
    }

    private sealed class TestRabbitConsumer(IRabbitMqConnection conn)
        : RabbitMqConsumer<TestRabbitContent>(conn, NullLogger<RabbitMqConsumer<TestRabbitContent>>.Instance)
    {
        public override void AddHandler() => Handler = _ => Task.CompletedTask;
    }

    #endregion
}
