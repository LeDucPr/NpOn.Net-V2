using System.Diagnostics;
using Cassandra;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.CassandraFactory;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;
using Common.Infrastructures.NpOn.CassandraExtCm.Results;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using Xunit;
using Xunit.Abstractions;

namespace NpOn.Core.Tests;

[Trait("Category", "Integration")]
[Trait("Category", "IntegrationScylla")]
public class ScyllaClusterDriverTests(ITestOutputHelper output)
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

    [Fact]
    public async Task ScyllaDriver_ConnectAsync_ShouldEstablishValidClusterSession()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);

        // Act
        await driver.ConnectAsync(CancellationToken.None);

        // Assert
        Assert.True(driver.IsValidSession);
        output.WriteLine($"[PASS] Ket noi thanh cong toi ScyllaDB tai {ClusterHost}:{ClusterPort}, Keyspace: {KeyspaceName}");

        await driver.DisconnectAsync();
    }

    [Fact]
    public async Task ScyllaDriver_QueryAccounts_ShouldReturnRealRecordsFromScarlight()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        var command = new NpOnDbCommand(
            EDb.Cassandra,
            "SELECT id, username, password, full_name, email, phone_number, status FROM scarlight.acc_srv_account LIMIT 10;");

        // Act
        INpOnWrapperResult result = await driver.Execute(command);

        // Assert
        Assert.True(result.Status);

        var accounts = result.ToList<AccountRModel>();
        Assert.NotNull(accounts);
        Assert.NotEmpty(accounts);

        output.WriteLine($"[PASS] Doc thanh cong {accounts.Count} ban ghi tu bang scarlight.acc_srv_account");
        foreach (var acc in accounts.Take(3))
        {
            output.WriteLine($" - Account: Id={acc.Id}, Username={acc.UserName}, FullName={acc.FullName}, Phone={acc.PhoneNumber}, Status={acc.Status}");
        }

        await driver.DisconnectAsync();
    }

    [Fact]
    public async Task ScyllaDriver_CrudOperations_ShouldSucceedOnCluster()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        var testId = Guid.NewGuid();
        var testUsername = $"test_bot_{testId:N}".Substring(0, 16);

        // 1. INSERT
        var insertCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "INSERT INTO scarlight.acc_srv_account (id, username, password, full_name, phone_number, status, created_at, updated_at) " +
            "VALUES (?, ?, ?, ?, ?, ?, toTimestamp(now()), toTimestamp(now()));",
            [
                new NpOnDbCommandParam<ECassandraDbType>("id", testId, ECassandraDbType.Uuid),
                new NpOnDbCommandParam<ECassandraDbType>("username", testUsername, ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("password", "test_hash_secret", ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("full_name", "Test Agent AI", ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("phone_number", "0999888777", ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("status", (int)EAccountStatus.Active, ECassandraDbType.Int)
            ]);

        var insertResult = await driver.Execute(insertCmd);
        Assert.True(insertResult.Status);
        output.WriteLine($"[PASS] INSERT thanh cong ban ghi test: {testId}");

        // 2. SELECT BY ID
        var selectCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "SELECT id, username, password, full_name, phone_number, status FROM scarlight.acc_srv_account WHERE id = ?;",
            [new NpOnDbCommandParam<ECassandraDbType>("id", testId, ECassandraDbType.Uuid)]);

        var selectResult = await driver.Execute(selectCmd);
        Assert.True(selectResult.Status);
        var fetched = selectResult.ToFirstOrDefault<AccountRModel>();
        Assert.NotNull(fetched);
        Assert.Equal(testUsername, fetched.UserName);
        Assert.Equal("Test Agent AI", fetched.FullName);
        output.WriteLine($"[PASS] SELECT BY PRIMARY KEY xac nhan: {fetched.UserName} - {fetched.FullName}");

        // 3. UPDATE
        var updateCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "UPDATE scarlight.acc_srv_account SET full_name = ?, status = ? WHERE id = ?;",
            [
                new NpOnDbCommandParam<ECassandraDbType>("full_name", "Test Agent AI Updated", ECassandraDbType.Text),
                new NpOnDbCommandParam<ECassandraDbType>("status", (int)EAccountStatus.Unactive, ECassandraDbType.Int),
                new NpOnDbCommandParam<ECassandraDbType>("id", testId, ECassandraDbType.Uuid)
            ]);

        var updateResult = await driver.Execute(updateCmd);
        Assert.True(updateResult.Status);
        output.WriteLine("[PASS] UPDATE thanh cong ban ghi test");

        // 4. DELETE (Clean up)
        var deleteCmd = new NpOnDbCommand(
            EDb.Cassandra,
            "DELETE FROM scarlight.acc_srv_account WHERE id = ?;",
            [new NpOnDbCommandParam<ECassandraDbType>("id", testId, ECassandraDbType.Uuid)]);

        var deleteResult = await driver.Execute(deleteCmd);
        Assert.True(deleteResult.Status);
        output.WriteLine("[PASS] DELETE don dep ban ghi test hoan tat");

        await driver.DisconnectAsync();
    }

    [Fact]
    public async Task ScyllaDriver_RealWorldPerformance_PointLookupBenchmark()
    {
        // Arrange
        var option = CreateOption();
        var driver = new CassandraDriver(option);
        await driver.ConnectAsync(CancellationToken.None);

        // 1. Lay danh sach 100 Account ID thuc te trong scarlight
        var seedCmd = new NpOnDbCommand(EDb.Cassandra, "SELECT id, username, password FROM scarlight.acc_srv_account LIMIT 100;");
        var seedResult = await driver.Execute(seedCmd);
        Assert.True(seedResult.Status);
        var sampleIds = seedResult.ToList<AccountRModel>()?.Select(a => a.Id).ToList();
        Assert.NotNull(sampleIds);
        Assert.NotEmpty(sampleIds);

        output.WriteLine($"[INFO] Da lay {sampleIds.Count} ID mau tu scarlight de chay Point-Lookup Benchmark");

        // 2. Chay 1,000 Point-Lookup queries lien tuc
        const int iterations = 1000;
        var latencies = new List<double>(iterations);
        var swTotal = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var targetId = sampleIds[i % sampleIds.Count];
            var queryCmd = new NpOnDbCommand(
                EDb.Cassandra,
                "SELECT id, username, password, full_name, email, phone_number, status FROM scarlight.acc_srv_account WHERE id = ?;",
                [new NpOnDbCommandParam<ECassandraDbType>("id", targetId, ECassandraDbType.Uuid)]);

            var swQuery = Stopwatch.StartNew();
            var res = await driver.Execute(queryCmd);
            swQuery.Stop();

            Assert.True(res.Status);
            latencies.Add(swQuery.Elapsed.TotalMilliseconds);
        }

        swTotal.Stop();

        // 3. Tinh toan Metrics P50, P95, P99, Throughput
        latencies.Sort();
        double p50 = latencies[(int)(iterations * 0.50)];
        double p95 = latencies[(int)(iterations * 0.95)];
        double p99 = latencies[(int)(iterations * 0.99)];
        double avg = latencies.Average();
        double opsPerSec = iterations / swTotal.Elapsed.TotalSeconds;

        output.WriteLine("================================================================================");
        output.WriteLine(" SCYLLADB CLUSTER POINT-LOOKUP PERFORMANCE BENCHMARK (1,000 QUERIES)");
        output.WriteLine("================================================================================");
        output.WriteLine($" - Tong thoi gian:    {swTotal.ElapsedMilliseconds} ms");
        output.WriteLine($" - Throughput:        {opsPerSec:F2} queries/sec");
        output.WriteLine($" - Do tre Trung binh: {avg:F3} ms");
        output.WriteLine($" - Do tre P50:        {p50:F3} ms");
        output.WriteLine($" - Do tre P95:        {p95:F3} ms");
        output.WriteLine($" - Do tre P99:        {p99:F3} ms");
        output.WriteLine("================================================================================");

        Assert.True(opsPerSec > 100, $"Throughput {opsPerSec} phai vuot tren 100 op/s");

        await driver.DisconnectAsync();
    }
}
