using System.Diagnostics;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Dapper;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using Npgsql;

namespace NpOn.OmsDapper;

public class BenchmarkRunner(string connectionString, IPostgresFactoryWrapper udalWrapper, int taskId = -1)
{
    private static readonly object ConsoleLock = new();
    private readonly ConsoleColor _taskColor = taskId == -1 ? ConsoleColor.White : (ConsoleColor)((taskId % 14) + 1);

    private void Log(string message)
    {
        lock (ConsoleLock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = _taskColor;
            string prefix = taskId == -1 ? "[Single]" : $"[Task {taskId:D2}]";
            Console.WriteLine($"{prefix} {message}");
            Console.ForegroundColor = originalColor;
        }
    }

    public async Task Warmup()
    {
        Log("################### Warmup First ###################");
        using var conn = new NpgsqlConnection(connectionString);
        await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM acc_srv_account");
        await udalWrapper.ExecuteAsync(new NpOnDbCommand(EDb.Postgres, "SELECT COUNT(*) FROM acc_srv_account"));
    }

    // --- NEW: Dapper Only Benchmark ---
    public async Task RunDapperBenchmarkOnly(bool isMultiTaskRun = false)
    {
        if (!isMultiTaskRun) Log("--- [DAPPER ONLY] BENCHMARK (20k+ records) ---");
        
        var sw = Stopwatch.StartNew();
        using var conn = new NpgsqlConnection(connectionString);
        var dapperList = (await conn.QueryAsync<AccountRModel>("SELECT * FROM acc_srv_account")).ToList();
        sw.Stop();
        
        Log($"[Dapper] Records: {dapperList.Count} | Time: {sw.Elapsed.TotalMilliseconds:F2}ms");
    }

    // --- NEW: UDAL Only Benchmark ---
    public async Task RunUdalBenchmarkOnly(bool isMultiTaskRun = false)
    {
        if (!isMultiTaskRun) Log("--- [UDAL ONLY] BENCHMARK (20k+ records) ---");
        
        var sw = Stopwatch.StartNew();
        var udalResult = await udalWrapper.ExecuteAsync(new NpOnDbCommand(EDb.Postgres, "SELECT * FROM acc_srv_account"));
        var udalList = udalResult.ToList<AccountRModel>();
        sw.Stop();
        
        Log($"[UDAL]   Records: {udalList?.Count} | Time: {sw.Elapsed.TotalMilliseconds:F2}ms");
    }

    public async Task RunCountBenchmark()
    {
        Log("--- [1] BENCHMARK: COUNT(*) ---");

        var sw = Stopwatch.StartNew();
        using var conn = new NpgsqlConnection(connectionString);
        var dapperCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM acc_srv_account");
        sw.Stop();
        Log($"[Dapper] Count: {dapperCount} | Time: {sw.Elapsed.TotalMilliseconds:F2}ms");

        sw.Restart();
        await udalWrapper.ExecuteAsync(new NpOnDbCommand(EDb.Postgres, "SELECT COUNT(*) FROM acc_srv_account"));
        sw.Stop();
        Log($"[UDAL]   Count: {dapperCount} | Time: {sw.Elapsed.TotalMilliseconds:F2}ms (Query only)");
    }

    public async Task RunMappingBenchmark(bool isMultiTaskRun = false)
    {
        Log("--- [2] BENCHMARK: SELECT * & MAP (20k+ records) ---");

        if (!isMultiTaskRun)
        {
            Log("Warming up...");
            await Warmup();
        }

        Log("Starting official measurement...");
        await RunMappingOnce(true, isMultiTaskRun);
    }

    private async Task RunMappingOnce(bool printResult, bool isMultiTaskRun)
    {
        // 1. Dapper Mapping
        var sw = Stopwatch.StartNew();
        using var conn = new NpgsqlConnection(connectionString);
        var dapperList = (await conn.QueryAsync<AccountRModel>("SELECT * FROM acc_srv_account")).ToList();
        sw.Stop();
        var dapperTime = sw.Elapsed.TotalMilliseconds;

        // 2. UDAL Mapping (Zero Allocation with ObjectPooling)
        sw.Restart();
        var udalResult = await udalWrapper.ExecuteAsync(new NpOnDbCommand(EDb.Postgres, "SELECT * FROM acc_srv_account"));
        var udalList = udalResult.ToList<AccountRModel>();
        sw.Stop();
        var udalTime = sw.Elapsed.TotalMilliseconds;

        if (printResult)
        {
            Log($"[Dapper] Records: {dapperList.Count} | Time: {dapperTime:F2}ms");
            Log($"[UDAL]   Records: {udalList?.Count} | Time: {udalTime:F2}ms");

            double diff = dapperTime - udalTime;
            string winner = diff > 0 ? "UDAL" : "Dapper";
            Log($"==> Result: {winner} is faster by {Math.Abs(diff):F2}ms");
        }
    }
}