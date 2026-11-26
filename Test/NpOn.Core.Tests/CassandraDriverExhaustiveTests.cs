using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Numerics;
using Cassandra;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;
using Common.Infrastructures.NpOn.CassandraExtCm.Results;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using Xunit;
using Xunit.Abstractions;

namespace NpOn.Core.Tests;

#region Test Models

public class CassandraAllTypesRModel : NpOnBaseGrpcObject
{
    public Guid ColUuid { get; set; }
    public string? ColAscii { get; set; }
    public long ColBigint { get; set; }
    public byte[]? ColBlob { get; set; }
    public bool ColBoolean { get; set; }
    public DateTime ColDate { get; set; }
    public decimal ColDecimal { get; set; }
    public double ColDouble { get; set; }
    public float ColFloat { get; set; }
    public string? ColInet { get; set; }
    public int ColInt { get; set; }
    public short ColSmallint { get; set; }
    public sbyte ColTinyint { get; set; }
    public TimeSpan ColTime { get; set; }
    public DateTimeOffset ColTimestamp { get; set; }
    public Guid ColTimeuuid { get; set; }
    public string? ColText { get; set; }
    public string? ColVarchar { get; set; }
    public string? ColVarint { get; set; }
    public List<string>? ColList { get; set; }
    public HashSet<int>? ColSet { get; set; }
    public Dictionary<string, int>? ColMap { get; set; }

    public override Dictionary<string, string>? FieldMap { get; protected set; }

    public CassandraAllTypesRModel()
    {
        CreateDefaultFieldMapper();
    }

    protected override void FieldMapper()
    {
        FieldMap ??= new(StringComparer.OrdinalIgnoreCase);
        FieldMap[nameof(ColUuid)] = "col_uuid";
        FieldMap[nameof(ColAscii)] = "col_ascii";
        FieldMap[nameof(ColBigint)] = "col_bigint";
        FieldMap[nameof(ColBlob)] = "col_blob";
        FieldMap[nameof(ColBoolean)] = "col_boolean";
        FieldMap[nameof(ColDate)] = "col_date";
        FieldMap[nameof(ColDecimal)] = "col_decimal";
        FieldMap[nameof(ColDouble)] = "col_double";
        FieldMap[nameof(ColFloat)] = "col_float";
        FieldMap[nameof(ColInet)] = "col_inet";
        FieldMap[nameof(ColInt)] = "col_int";
        FieldMap[nameof(ColSmallint)] = "col_smallint";
        FieldMap[nameof(ColTinyint)] = "col_tinyint";
        FieldMap[nameof(ColTime)] = "col_time";
        FieldMap[nameof(ColTimestamp)] = "col_timestamp";
        FieldMap[nameof(ColTimeuuid)] = "col_timeuuid";
        FieldMap[nameof(ColText)] = "col_text";
        FieldMap[nameof(ColVarchar)] = "col_varchar";
        FieldMap[nameof(ColVarint)] = "col_varint";
        FieldMap[nameof(ColList)] = "col_list";
        FieldMap[nameof(ColSet)] = "col_set";
        FieldMap[nameof(ColMap)] = "col_map";
    }
}

public class CassandraAllTypesNullableRModel : NpOnBaseGrpcObject
{
    public override Dictionary<string, string>? FieldMap { get; protected set; }

    public Guid ColUuid { get; set; }
    public string? ColAscii { get; set; }
    public long? ColBigint { get; set; }
    public byte[]? ColBlob { get; set; }
    public bool? ColBoolean { get; set; }
    public DateTime? ColDate { get; set; }
    public decimal? ColDecimal { get; set; }
    public double? ColDouble { get; set; }
    public float? ColFloat { get; set; }
    public string? ColInet { get; set; }
    public int? ColInt { get; set; }
    public short? ColSmallint { get; set; }
    public sbyte? ColTinyint { get; set; }
    public TimeSpan? ColTime { get; set; }
    public DateTimeOffset? ColTimestamp { get; set; }
    public Guid? ColTimeuuid { get; set; }
    public string? ColText { get; set; }
    public string? ColVarchar { get; set; }
    public string? ColVarint { get; set; }
    public List<string>? ColList { get; set; }
    public HashSet<int>? ColSet { get; set; }
    public Dictionary<string, int>? ColMap { get; set; }

    public CassandraAllTypesNullableRModel()
    {
        CreateDefaultFieldMapper();
    }

    protected override void FieldMapper()
    {
        FieldMap ??= new(StringComparer.OrdinalIgnoreCase);
        FieldMap[nameof(ColUuid)] = "col_uuid";
        FieldMap[nameof(ColAscii)] = "col_ascii";
        FieldMap[nameof(ColBigint)] = "col_bigint";
        FieldMap[nameof(ColBlob)] = "col_blob";
        FieldMap[nameof(ColBoolean)] = "col_boolean";
        FieldMap[nameof(ColDate)] = "col_date";
        FieldMap[nameof(ColDecimal)] = "col_decimal";
        FieldMap[nameof(ColDouble)] = "col_double";
        FieldMap[nameof(ColFloat)] = "col_float";
        FieldMap[nameof(ColInet)] = "col_inet";
        FieldMap[nameof(ColInt)] = "col_int";
        FieldMap[nameof(ColSmallint)] = "col_smallint";
        FieldMap[nameof(ColTinyint)] = "col_tinyint";
        FieldMap[nameof(ColTime)] = "col_time";
        FieldMap[nameof(ColTimestamp)] = "col_timestamp";
        FieldMap[nameof(ColTimeuuid)] = "col_timeuuid";
        FieldMap[nameof(ColText)] = "col_text";
        FieldMap[nameof(ColVarchar)] = "col_varchar";
        FieldMap[nameof(ColVarint)] = "col_varint";
        FieldMap[nameof(ColList)] = "col_list";
        FieldMap[nameof(ColSet)] = "col_set";
        FieldMap[nameof(ColMap)] = "col_map";
    }
}

#endregion

public class CassandraDriverExhaustiveTests(ITestOutputHelper output)
{
    private const string ClusterHost = "127.0.0.1";
    private const int ClusterPort = 9042;
    private const string KeyspaceName = "scarlight";

    private CassandraConnectOption CreateOption()
    {
        var opt = new CassandraConnectOption();
        opt.SetConnectionString(ClusterHost);
        opt.SetContactAddresses([ClusterHost]);
        opt.SetKeyspace(KeyspaceName);
        opt.SetPort<CassandraDriver>(ClusterPort);
        return opt;
    }

    #region 1. Static Type Caching & Utilities (Mirroring Postgres Tests)

    [Fact]
    public void CassandraCell_StaticTypeCaching_ShouldProduceAccurateDbType()
    {
        // Act
        var cellInt = CassandraCell<int>.FromCassandra(42, "int", false);
        var cellGuid = CassandraCell<Guid>.FromCassandra(Guid.NewGuid(), "uuid", true);
        var cellString = CassandraCell<string>.FromCassandra("hello", "text", false);
        var cellLong = CassandraCell<long>.FromCassandra(100L, "bigint", false);

        // Assert
        Assert.Equal(DbType.Int32, cellInt.DbType);
        Assert.Equal(42, cellInt.Value);
        Assert.False(cellInt.IsPrimaryKey);

        Assert.Equal(DbType.Guid, cellGuid.DbType);
        Assert.True(cellGuid.IsPrimaryKey);

        Assert.Equal(DbType.String, cellString.DbType);
        Assert.Equal("hello", cellString.Value);

        Assert.Equal(DbType.Int64, cellLong.DbType);
        Assert.Equal(100L, cellLong.Value);
    }

    [Fact]
    public void CassandraUtils_NormalizeForCassandra_ShouldReturnAccurateCassandraDbType()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var testDate = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);
        var ip = IPAddress.Parse("127.0.0.1");

        // Act
        var (valInt, typeInt) = CassandraUtils.NormalizeForCassandra(100);
        var (valGuid, typeGuid) = CassandraUtils.NormalizeForCassandra(testId);
        var (valDate, typeDate) = CassandraUtils.NormalizeForCassandra(testDate);
        var (valStr, typeStr) = CassandraUtils.NormalizeForCassandra("npon");
        var (valIp, typeIp) = CassandraUtils.NormalizeForCassandra(ip);

        // Assert
        Assert.Equal(ECassandraDbType.Int, typeInt);
        Assert.Equal(100, valInt);

        Assert.Equal(ECassandraDbType.Uuid, typeGuid);
        Assert.Equal(testId, valGuid);

        Assert.Equal(ECassandraDbType.Timestamp, typeDate);
        Assert.Equal(testDate, valDate);

        Assert.Equal(ECassandraDbType.Text, typeStr);
        Assert.Equal("npon", valStr);

        Assert.Equal(ECassandraDbType.Inet, typeIp);
        Assert.Equal(ip, valIp);
    }

    [Fact]
    public void CassandraMappingExtensions_CreateRowMapper_ShouldCacheCompiledDelegate()
    {
        // Arrange
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["id"] = new("id", typeof(Guid), "uuid", true),
            ["username"] = new("username", typeof(string), "text", false),
            ["score"] = new("score", typeof(int), "int", false)
        };
        var nameToIndexMap = new Dictionary<string, int>
        {
            ["id"] = 0,
            ["username"] = 1,
            ["score"] = 2
        };

        // Act: Generate twice
        var mapper1 = CassandraMappingExtensions.GetOrCreateRowMapper(schemaMap, nameToIndexMap);
        var mapper2 = CassandraMappingExtensions.GetOrCreateRowMapper(schemaMap, nameToIndexMap);

        // Assert: Both instances must be the exact same cached delegate (zero re-compilation)
        Assert.Same(mapper1, mapper2);

        // Execute delegate to verify IL bytecode execution correctness
        var expectedId = Guid.NewGuid();
        var rowData = new object[] { expectedId, "kha_banh_scylla", 999 };
        var cells = mapper1(rowData);

        Assert.NotNull(cells);
        Assert.Equal(3, cells.Count);
        Assert.Equal(expectedId, cells["id"].ValueAsObject);
        Assert.Equal("kha_banh_scylla", cells["username"].ValueAsObject);
        Assert.Equal(999, cells["score"].ValueAsObject);
    }

    [Fact]
    public void Cassandra_MultiThreaded_ConcurrentMapping_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int iterationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();

        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["id"] = new("id", typeof(Guid), "uuid", true),
            ["name"] = new("name", typeof(string), "text", false),
            ["count"] = new("count", typeof(int), "int", false)
        };
        var nameToIndexMap = new Dictionary<string, int>
        {
            ["id"] = 0,
            ["name"] = 1,
            ["count"] = 2
        };

        // Act: Run concurrently from multiple threads
        Parallel.For(0, threadCount, threadIdx =>
        {
            try
            {
                var mapper = CassandraMappingExtensions.GetOrCreateRowMapper(schemaMap, nameToIndexMap);
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var id = Guid.NewGuid();
                    var data = new object[] { id, $"scylla_user_{threadIdx}_{i}", i };
                    var cells = mapper(data);

                    if (!id.Equals(cells["id"].ValueAsObject) ||
                        !$"scylla_user_{threadIdx}_{i}".Equals(cells["name"].ValueAsObject) ||
                        !i.Equals(cells["count"].ValueAsObject))
                    {
                        throw new InvalidOperationException("Data corrupted during concurrent Cassandra mapping");
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        Assert.Empty(exceptions);
    }

    #endregion

    #region 2. High Concurrency & Stress Queries (Mirroring Postgres AuthenticationFlowTests)

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationScylla")]
    public async Task Cassandra_AccountQuery_ShouldSucceed100TimesWithoutFailure()
    {
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        const string cql = "SELECT id, username, password, full_name, email, phone_number, status FROM scarlight.acc_srv_account LIMIT 1;";

        for (int i = 0; i < 100; i++)
        {
            var cmd = new NpOnDbCommand(EDb.Cassandra, cql);
            var result = await driver.Execute(cmd);

            Assert.NotNull(result);
            Assert.True(result.Status);

            var account = result.ToFirstOrDefault<AccountRModel>();
            Assert.NotNull(account);
            Assert.NotEqual(Guid.Empty, account.Id);
            Assert.NotEmpty(account.UserName);
        }

        output.WriteLine("[PASS] Cassandra_AccountQuery_ShouldSucceed100TimesWithoutFailure hoan tat 100/100");
        await driver.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationScylla")]
    public async Task Cassandra_AccountQuery_Concurrent50Tasks_ShouldAllSucceed()
    {
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        const string cql = "SELECT id, username, password, full_name, email, phone_number, status FROM scarlight.acc_srv_account LIMIT 1;";

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            var cmd = new NpOnDbCommand(EDb.Cassandra, cql);
            var result = await driver.Execute(cmd);

            Assert.NotNull(result);
            Assert.True(result.Status);

            var account = result.ToFirstOrDefault<AccountRModel>();
            Assert.NotNull(account);
            Assert.NotEqual(Guid.Empty, account.Id);
            Assert.NotEmpty(account.UserName);
        });

        await Task.WhenAll(tasks);
        output.WriteLine("[PASS] Cassandra_AccountQuery_Concurrent50Tasks_ShouldAllSucceed hoan tat 50/50");
        await driver.DisconnectAsync();
    }

    #endregion

    #region 3. Full Data Types Live Cluster Verification (All 22 CQL Types)

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationScylla")]
    public async Task CassandraDriver_AllDataTypes_NonNullValues_ShouldInsertQueryAndMapSuccessfully()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        var testId = Guid.NewGuid();
        var testTimeUuid = TimeUuid.NewId();
        var testDate = new LocalDate(2026, 11, 19);
        var testTime = new LocalTime(14, 30, 0, 0);
        var testTimestamp = DateTimeOffset.UtcNow;
        var testIp = IPAddress.Parse("10.0.0.42");
        var testBlob = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var testVarint = BigInteger.Parse("99999999999999999999");
        var testList = new List<string> { "item1", "item2", "item3" };
        var testSet = new HashSet<int> { 100, 200, 300 };
        var testMap = new Dictionary<string, int> { ["alpha"] = 1, ["beta"] = 2 };

        // 1. INSERT Full Non-Null Row
        var insertCql = @"
INSERT INTO scarlight.test_all_types (
    col_uuid, col_ascii, col_bigint, col_blob, col_boolean,
    col_date, col_decimal, col_double, col_float, col_inet,
    col_int, col_smallint, col_tinyint, col_time, col_timestamp,
    col_timeuuid, col_text, col_varchar, col_varint, col_list, col_set, col_map
) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";

        var insertCmd = new NpOnDbCommand(
            EDb.Cassandra,
            insertCql,
            [
                new NpOnDbCommandParam<ECassandraDbType>("col_uuid", testId, ECassandraDbType.Uuid),
                new NpOnDbCommandParam<ECassandraDbType>("col_ascii", "ascii_val", ECassandraDbType.Ascii),
                new NpOnDbCommandParam<ECassandraDbType>("col_bigint", 123456789012345L, ECassandraDbType.Bigint),
                new NpOnDbCommandParam<ECassandraDbType>("col_blob", testBlob, ECassandraDbType.Blob),
                new NpOnDbCommandParam<ECassandraDbType>("col_boolean", true, ECassandraDbType.Boolean),
                new NpOnDbCommandParam<ECassandraDbType>("col_date", testDate, ECassandraDbType.Date),
                new NpOnDbCommandParam<ECassandraDbType>("col_decimal", 9876.54321m, ECassandraDbType.Decimal),
                new NpOnDbCommandParam<ECassandraDbType>("col_double", 3.1415926535, ECassandraDbType.Double),
                new NpOnDbCommandParam<ECassandraDbType>("col_float", 2.71828f, ECassandraDbType.Float),
                new NpOnDbCommandParam<ECassandraDbType>("col_inet", testIp, ECassandraDbType.Inet),
                new NpOnDbCommandParam<ECassandraDbType>("col_int", 777, ECassandraDbType.Int),
                new NpOnDbCommandParam<ECassandraDbType>("col_smallint", (short)32000, ECassandraDbType.SmallInt),
                new NpOnDbCommandParam<ECassandraDbType>("col_tinyint", (sbyte)120, ECassandraDbType.TinyInt),
                new NpOnDbCommandParam<ECassandraDbType>("col_time", testTime, ECassandraDbType.Time),
                new NpOnDbCommandParam<ECassandraDbType>("col_timestamp", testTimestamp, ECassandraDbType.Timestamp),
                new NpOnDbCommandParam<ECassandraDbType>("col_timeuuid", testTimeUuid, ECassandraDbType.Timeuuid),
                new NpOnDbCommandParam<ECassandraDbType>("col_text", "Sample Text Content", ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("col_varchar", "Varchar Value", ECassandraDbType.Varchar),
                new NpOnDbCommandParam<ECassandraDbType>("col_varint", testVarint, ECassandraDbType.Varint),
                new NpOnDbCommandParam<ECassandraDbType>("col_list", testList, ECassandraDbType.List),
                new NpOnDbCommandParam<ECassandraDbType>("col_set", testSet, ECassandraDbType.Set),
                new NpOnDbCommandParam<ECassandraDbType>("col_map", testMap, ECassandraDbType.Map)
            ]);

        var insertRes = await driver.Execute(insertCmd);
        Assert.True(insertRes.Status);
        output.WriteLine($"[PASS] Insert day du 22 kieu du lieu thanh cong voi PK={testId}");

        // 2. SELECT and MAP via Dynamic IL Emit
        var selectCql = "SELECT * FROM scarlight.test_all_types WHERE col_uuid = ?;";
        var selectCmd = new NpOnDbCommand(
            EDb.Cassandra,
            selectCql,
            [new NpOnDbCommandParam<ECassandraDbType>("col_uuid", testId, ECassandraDbType.Uuid)]);

        var selectRes = await driver.Execute(selectCmd);
        Assert.True(selectRes.Status);

        var model = selectRes.ToFirstOrDefault<CassandraAllTypesRModel>();
        Assert.NotNull(model);

        // Verify Assertions for ALL Types
        Assert.Equal(testId, model.ColUuid);
        Assert.Equal("ascii_val", model.ColAscii);
        Assert.Equal(123456789012345L, model.ColBigint);
        Assert.Equal(testBlob, model.ColBlob);
        Assert.True(model.ColBoolean);
        Assert.Equal(new DateTime(2026, 11, 19), model.ColDate);
        Assert.Equal(9876.54321m, model.ColDecimal);
        Assert.Equal(3.1415926535, model.ColDouble, precision: 8);
        Assert.Equal(2.71828f, model.ColFloat, precision: 4);
        Assert.Equal("10.0.0.42", model.ColInet);
        Assert.Equal(777, model.ColInt);
        Assert.Equal((short)32000, model.ColSmallint);
        Assert.Equal((sbyte)120, model.ColTinyint);
        Assert.Equal(testTime.Hour, model.ColTime.Hours);
        Assert.Equal((Guid)testTimeUuid, model.ColTimeuuid);
        Assert.Equal("Sample Text Content", model.ColText);
        Assert.Equal("Varchar Value", model.ColVarchar);
        Assert.Equal(testVarint.ToString(), model.ColVarint);

        // Collections Assertions
        Assert.NotNull(model.ColList);
        Assert.Equal(["item1", "item2", "item3"], model.ColList);

        Assert.NotNull(model.ColSet);
        Assert.True(model.ColSet.SetEquals([100, 200, 300]));

        Assert.NotNull(model.ColMap);
        Assert.Equal(1, model.ColMap["alpha"]);
        Assert.Equal(2, model.ColMap["beta"]);

        output.WriteLine("[PASS] Dynamic IL Emit Object Mapping thanh cong 100% tren tat ca cac kieu du lieu!");

        // 3. Clean up
        var deleteCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "DELETE FROM scarlight.test_all_types WHERE col_uuid = ?;",
            [new NpOnDbCommandParam<ECassandraDbType>("col_uuid", testId, ECassandraDbType.Uuid)]);
        var delRes = await driver.Execute(deleteCmd);
        Assert.True(delRes.Status);

        await driver.DisconnectAsync();
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Category", "IntegrationScylla")]
    public async Task CassandraDriver_AllDataTypes_NullValues_ShouldHandleSafelyWithoutException()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        var nullTestId = Guid.NewGuid();

        // 1. INSERT Row with ONLY Primary Key (all other 21 columns are tombstone / null)
        var insertNullCql = "INSERT INTO scarlight.test_all_types (col_uuid) VALUES (?);";
        var insertNullCmd = new NpOnDbCommand(
            EDb.Cassandra,
            insertNullCql,
            [new NpOnDbCommandParam<ECassandraDbType>("col_uuid", nullTestId, ECassandraDbType.Uuid)]);

        var insertRes = await driver.Execute(insertNullCmd);
        Assert.True(insertRes.Status);
        output.WriteLine($"[PASS] Insert ban ghi toan bo gia tri NULL voi PK={nullTestId}");

        // 2. SELECT and MAP to Nullable Model (verifying zero NullReferenceException on Row.GetValue)
        var selectCql = "SELECT * FROM scarlight.test_all_types WHERE col_uuid = ?;";
        var selectCmd = new NpOnDbCommand(
            EDb.Cassandra,
            selectCql,
            [new NpOnDbCommandParam<ECassandraDbType>("col_uuid", nullTestId, ECassandraDbType.Uuid)]);

        var selectRes = await driver.Execute(selectCmd);
        Assert.True(selectRes.Status);

        var nullModel = selectRes.ToFirstOrDefault<CassandraAllTypesNullableRModel>();
        Assert.NotNull(nullModel);

        // Verify all fields are safely null without throw
        Assert.Equal(nullTestId, nullModel.ColUuid);
        Assert.Null(nullModel.ColAscii);
        Assert.Null(nullModel.ColBigint);
        Assert.Null(nullModel.ColBlob);
        Assert.Null(nullModel.ColBoolean);
        Assert.Null(nullModel.ColDate);
        Assert.Null(nullModel.ColDecimal);
        Assert.Null(nullModel.ColDouble);
        Assert.Null(nullModel.ColFloat);
        Assert.Null(nullModel.ColInet);
        Assert.Null(nullModel.ColInt);
        Assert.Null(nullModel.ColSmallint);
        Assert.Null(nullModel.ColTinyint);
        Assert.Null(nullModel.ColTime);
        Assert.Null(nullModel.ColTimestamp);
        Assert.Null(nullModel.ColTimeuuid);
        Assert.Null(nullModel.ColText);
        Assert.Null(nullModel.ColVarchar);
        Assert.Null(nullModel.ColVarint);
        Assert.Null(nullModel.ColList);
        Assert.Null(nullModel.ColSet);
        Assert.Null(nullModel.ColMap);

        output.WriteLine("[PASS] Xac nhan an toan tuyet doi: Toan bo cac truong gia tri NULL duoc anh xa an toan (Zero Exception)!");

        // 3. Clean up
        var deleteCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "DELETE FROM scarlight.test_all_types WHERE col_uuid = ?;",
            [new NpOnDbCommandParam<ECassandraDbType>("col_uuid", nullTestId, ECassandraDbType.Uuid)]);
        await driver.Execute(deleteCmd);

        await driver.DisconnectAsync();
    }

    #endregion

    #region 4. Dynamic IL Emit Mapping Performance Benchmark (10,000 iterations)

    [Fact]
    public void Cassandra_DynamicIL_RowMapper_Benchmark_10000Iterations_ShouldExceed10kOpsAndZeroGCLeak()
    {
        // Arrange
        const int iterations = 10000;
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["id"] = new("id", typeof(Guid), "uuid", true),
            ["username"] = new("username", typeof(string), "text", false),
            ["password"] = new("password", typeof(string), "text", false),
            ["full_name"] = new("full_name", typeof(string), "text", false),
            ["email"] = new("email", typeof(string), "text", false),
            ["phone_number"] = new("phone_number", typeof(string), "text", false),
            ["status"] = new("status", typeof(int), "int", false)
        };
        var nameToIndexMap = new Dictionary<string, int>
        {
            ["id"] = 0,
            ["username"] = 1,
            ["password"] = 2,
            ["full_name"] = 3,
            ["email"] = 4,
            ["phone_number"] = 5,
            ["status"] = 6
        };

        var mapper = CassandraMappingExtensions.GetOrCreateRowMapper(schemaMap, nameToIndexMap);
        var expectedId = Guid.NewGuid();
        var rowData = new object[]
        {
            expectedId,
            "test_bench_user",
            "hashed_pwd_123",
            "Nguyen Van Test",
            "test@npon.vn",
            "0901234567",
            1
        };

        // Warmup JIT
        for (int i = 0; i < 200; i++)
        {
            _ = mapper(rowData);
        }

        // Force GC for clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        long initialMemory = GC.GetTotalMemory(false);

        // Act: 10,000 mapping iterations on hot-path
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var cells = mapper(rowData);
            if (cells.Count != 7)
            {
                throw new InvalidOperationException("Mapping output corrupted during benchmark loop");
            }
        }
        sw.Stop();

        int finalGen0 = GC.CollectionCount(0) - initialGen0;
        int finalGen1 = GC.CollectionCount(1) - initialGen1;
        int finalGen2 = GC.CollectionCount(2) - initialGen2;
        long finalMemory = GC.GetTotalMemory(false);
        double memDeltaKb = Math.Round((finalMemory - initialMemory) / 1024.0, 2);
        double opsPerSec = Math.Round(iterations / (sw.Elapsed.TotalMilliseconds / 1000.0), 0);

        output.WriteLine($"[BENCHMARK] Cassandra Dynamic IL RowMapper ({iterations:N0} iterations):");
        output.WriteLine($"  - Total Duration: {sw.Elapsed.TotalMilliseconds:F2} ms");
        output.WriteLine($"  - Throughput: {opsPerSec:N0} operations/sec");
        output.WriteLine($"  - GC Collections: Gen0={finalGen0}, Gen1={finalGen1}, Gen2={finalGen2} (Zero GC Leak)");
        output.WriteLine($"  - Memory Delta: {memDeltaKb} KB");

        // Assert: Zero Gen2 leak & high throughput
        Assert.Equal(0, finalGen2);
        Assert.InRange(finalGen1, 0, 2);
        Assert.True(opsPerSec >= 10000, $"Throughput {opsPerSec} ops/sec is below required 10,000 ops/sec");
    }

    [Fact]
    public void Cassandra_DynamicIL_ObjectMapping_Benchmark_10000Iterations_ShouldHaveHighThroughputAndZeroGCLeak()
    {
        // Arrange
        const int iterations = 10000;
        var expectedId = Guid.NewGuid();
        var cells = new Dictionary<string, INpOnCell>
        {
            ["id"] = CassandraCell<Guid>.FromCassandra(expectedId, "uuid", true),
            ["username"] = CassandraCell<string>.FromCassandra("bench_account", "text", false),
            ["password"] = CassandraCell<string>.FromCassandra("p@ssword", "text", false),
            ["full_name"] = CassandraCell<string>.FromCassandra("Bench Account Name", "text", false),
            ["email"] = CassandraCell<string>.FromCassandra("bench@npon.vn", "text", false),
            ["phone_number"] = CassandraCell<string>.FromCassandra("0987654321", "text", false),
            ["status"] = CassandraCell<int>.FromCassandra((int)EAccountStatus.Active, "int", false),
            ["permission"] = CassandraCell<int>.FromCassandra((int)EPermission.Administrator, "int", false)
        };

        var rowWrapper = new MockRowWrapper(cells);
        var table = new MockTableWrapper([rowWrapper]);

        // Warmup IL mapper
        for (int i = 0; i < 200; i++)
        {
            _ = table.ToList<AccountRModel>();
        }

        // Force GC for clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        long initialMemory = GC.GetTotalMemory(false);

        // Act: 10,000 iterations mapping Cassandra cells to AccountRModel
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var list = table.ToList<AccountRModel>();
            if (list == null || list.Count != 1 || list[0]!.Id != expectedId)
            {
                throw new InvalidOperationException("Mapped object data invalid during benchmark");
            }
        }
        sw.Stop();

        int finalGen0 = GC.CollectionCount(0) - initialGen0;
        int finalGen1 = GC.CollectionCount(1) - initialGen1;
        int finalGen2 = GC.CollectionCount(2) - initialGen2;
        long finalMemory = GC.GetTotalMemory(false);
        double memDeltaKb = Math.Round((finalMemory - initialMemory) / 1024.0, 2);
        double opsPerSec = Math.Round(iterations / (sw.Elapsed.TotalMilliseconds / 1000.0), 0);

        output.WriteLine($"[BENCHMARK] Cassandra Dynamic IL Object Mapper ({iterations:N0} iterations):");
        output.WriteLine($"  - Total Duration: {sw.Elapsed.TotalMilliseconds:F2} ms");
        output.WriteLine($"  - Throughput: {opsPerSec:N0} operations/sec");
        output.WriteLine($"  - GC Collections: Gen0={finalGen0}, Gen1={finalGen1}, Gen2={finalGen2} (Zero GC Leak)");
        output.WriteLine($"  - Memory Delta: {memDeltaKb} KB");

        // Assert: Zero Gen2 leak and high throughput
        Assert.Equal(0, finalGen2);
        Assert.InRange(finalGen1, 0, 2);
        Assert.True(opsPerSec >= 10000, $"Throughput {opsPerSec} ops/sec is below 10,000 ops/sec");
    }

    #endregion
}
