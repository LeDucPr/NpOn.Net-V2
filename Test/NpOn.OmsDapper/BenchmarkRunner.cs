using System.Collections.Concurrent;
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

public class BenchmarkMetrics
{
    public string Name { get; set; } = string.Empty;
    public int Concurrency { get; set; }
    public int TotalQueries { get; set; }
    public int TotalRecords { get; set; }
    public double TotalDurationMs { get; set; }
    public double AvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double ThroughputRecordsPerSec { get; set; }
    public double ThroughputQueriesPerSec { get; set; }
    public int GcGen0 { get; set; }
    public int GcGen1 { get; set; }
    public int GcGen2 { get; set; }
    public double MemoryDeltaKb { get; set; }
    public double WorkingSetMb { get; set; }
}

public class AggregatedBenchmarkMetrics
{
    public string Name { get; set; } = string.Empty;
    public int Concurrency { get; set; }
    public int TotalRuns { get; set; }
    public int TotalQueriesPerRun { get; set; }
    public int TotalRecordsPerRun { get; set; }

    public double MeanTotalDurationMs { get; set; }
    public double MeanAvgLatencyMs { get; set; }
    public double MinLatencyMs { get; set; }
    public double MaxLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }

    public double MeanThroughputRecordsPerSec { get; set; }
    public double MeanThroughputQueriesPerSec { get; set; }

    public double MeanGcGen0 { get; set; }
    public double MeanGcGen1 { get; set; }
    public double MeanGcGen2 { get; set; }

    public double MeanMemoryDeltaKb { get; set; }
    public double MeanWorkingSetMb { get; set; }
}

public class BenchmarkRunner
{
    private readonly string _connectionString;
    private readonly IPostgresFactoryWrapper _udalWrapper;
    private static readonly object ConsoleLock = new();

    static BenchmarkRunner()
    {
        // Enable snake_case to PascalCase matching for Dapper
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    public BenchmarkRunner(string connectionString, IPostgresFactoryWrapper udalWrapper)
    {
        _connectionString = connectionString;
        _udalWrapper = udalWrapper;
    }

    public void Log(string message, ConsoleColor color = ConsoleColor.White)
    {
        lock (ConsoleLock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ForegroundColor = originalColor;
        }
    }

    public async Task WarmupAsync(int maxConcurrency = 100)
    {
        Log($"[WARMUP] Warming up JIT & pre-opening database connections ({maxConcurrency} concurrent)...", ConsoleColor.Cyan);

        // Pre-warm Dapper pool
        var dTasks = new Task[maxConcurrency];
        for (int i = 0; i < maxConcurrency; i++)
        {
            dTasks[i] = Task.Run(async () =>
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var dapperList = (await conn.QueryAsync<AccountRModel>("SELECT * FROM acc_srv_account LIMIT 1")).ToList();
            });
        }
        await Task.WhenAll(dTasks);

        // Pre-warm UDAL pool
        var uTasks = new Task[maxConcurrency];
        for (int i = 0; i < maxConcurrency; i++)
        {
            uTasks[i] = Task.Run(async () =>
            {
                var udalList = await _udalWrapper.QueryAsync<AccountRModel>(new NpOnDbCommand(EDb.Postgres, "SELECT * FROM acc_srv_account LIMIT 1"));
            });
        }
        await Task.WhenAll(uTasks);

        Log("[WARMUP] Completed successfully.\n", ConsoleColor.Green);
    }

    public async Task<BenchmarkMetrics> RunSingleSequentialAsync(string framework, int iterations)
    {
        ForceGc();
        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        long initialMemory = GC.GetTotalMemory(false);

        var latencies = new List<double>(iterations);
        int totalRecords = 0;
        var overallSw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            if (framework.Equals("Dapper", StringComparison.OrdinalIgnoreCase))
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();
                var list = (await conn.QueryAsync<AccountRModel>("SELECT * FROM acc_srv_account")).ToList();
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                totalRecords += list.Count;
            }
            else
            {
                var list = await _udalWrapper.QueryAsync<AccountRModel>(new NpOnDbCommand(EDb.Postgres, "SELECT * FROM acc_srv_account"));
                sw.Stop();
                latencies.Add(sw.Elapsed.TotalMilliseconds);
                totalRecords += list.Count;
            }
        }

        overallSw.Stop();
        long finalMemory = GC.GetTotalMemory(false);

        return new BenchmarkMetrics
        {
            Name = framework,
            Concurrency = 1,
            TotalQueries = iterations,
            TotalRecords = totalRecords,
            TotalDurationMs = overallSw.Elapsed.TotalMilliseconds,
            AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : 0,
            MinLatencyMs = latencies.Count > 0 ? latencies.Min() : 0,
            MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : 0,
            ThroughputRecordsPerSec = Math.Round(totalRecords / (overallSw.Elapsed.TotalMilliseconds / 1000.0), 2),
            ThroughputQueriesPerSec = Math.Round(iterations / (overallSw.Elapsed.TotalMilliseconds / 1000.0), 2),
            GcGen0 = GC.CollectionCount(0) - initialGen0,
            GcGen1 = GC.CollectionCount(1) - initialGen1,
            GcGen2 = GC.CollectionCount(2) - initialGen2,
            MemoryDeltaKb = Math.Round((finalMemory - initialMemory) / 1024.0, 2),
            WorkingSetMb = Math.Round(Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0), 2)
        };
    }

    public async Task<BenchmarkMetrics> RunConcurrentBenchmarkAsync(string framework, int concurrency)
    {
        ForceGc();
        int initialGen0 = GC.CollectionCount(0);
        int initialGen1 = GC.CollectionCount(1);
        int initialGen2 = GC.CollectionCount(2);
        long initialMemory = GC.GetTotalMemory(false);

        var latencies = new ConcurrentBag<double>();
        var recordsCount = new ConcurrentBag<int>();
        var overallSw = Stopwatch.StartNew();

        var tasks = new Task[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                if (framework.Equals("Dapper", StringComparison.OrdinalIgnoreCase))
                {
                    await using var conn = new NpgsqlConnection(_connectionString);
                    await conn.OpenAsync();
                    var list = (await conn.QueryAsync<AccountRModel>("SELECT * FROM acc_srv_account")).ToList();
                    sw.Stop();
                    latencies.Add(sw.Elapsed.TotalMilliseconds);
                    recordsCount.Add(list.Count);
                }
                else
                {
                    var list = await _udalWrapper.QueryAsync<AccountRModel>(new NpOnDbCommand(EDb.Postgres, "SELECT * FROM acc_srv_account"));
                    sw.Stop();
                    latencies.Add(sw.Elapsed.TotalMilliseconds);
                    recordsCount.Add(list.Count);
                }
            });
        }

        await Task.WhenAll(tasks);
        overallSw.Stop();
        long finalMemory = GC.GetTotalMemory(false);

        int totalRecords = recordsCount.Sum();
        return new BenchmarkMetrics
        {
            Name = framework,
            Concurrency = concurrency,
            TotalQueries = concurrency,
            TotalRecords = totalRecords,
            TotalDurationMs = overallSw.Elapsed.TotalMilliseconds,
            AvgLatencyMs = latencies.Count > 0 ? latencies.Average() : 0,
            MinLatencyMs = latencies.Count > 0 ? latencies.Min() : 0,
            MaxLatencyMs = latencies.Count > 0 ? latencies.Max() : 0,
            ThroughputRecordsPerSec = Math.Round(totalRecords / (overallSw.Elapsed.TotalMilliseconds / 1000.0), 2),
            ThroughputQueriesPerSec = Math.Round(concurrency / (overallSw.Elapsed.TotalMilliseconds / 1000.0), 2),
            GcGen0 = GC.CollectionCount(0) - initialGen0,
            GcGen1 = GC.CollectionCount(1) - initialGen1,
            GcGen2 = GC.CollectionCount(2) - initialGen2,
            MemoryDeltaKb = Math.Round((finalMemory - initialMemory) / 1024.0, 2),
            WorkingSetMb = Math.Round(Process.GetCurrentProcess().WorkingSet64 / (1024.0 * 1024.0), 2)
        };
    }

    public async Task<(AggregatedBenchmarkMetrics Dapper, AggregatedBenchmarkMetrics Udal)> RunPairedSequentialBenchmarkAsync(
        int iterationsPerRun,
        int totalRuns,
        Action<int, int, BenchmarkMetrics, BenchmarkMetrics>? onProgress = null)
    {
        var dapperTrials = new List<BenchmarkMetrics>(totalRuns);
        var udalTrials = new List<BenchmarkMetrics>(totalRuns);

        for (int run = 1; run <= totalRuns; run++)
        {
            BenchmarkMetrics dapperM;
            BenchmarkMetrics udalM;

            if (run % 2 == 1)
            {
                dapperM = await RunSingleSequentialAsync("Dapper", iterationsPerRun);
                udalM = await RunSingleSequentialAsync("UDAL", iterationsPerRun);
            }
            else
            {
                udalM = await RunSingleSequentialAsync("UDAL", iterationsPerRun);
                dapperM = await RunSingleSequentialAsync("Dapper", iterationsPerRun);
            }

            dapperTrials.Add(dapperM);
            udalTrials.Add(udalM);

            onProgress?.Invoke(run, totalRuns, dapperM, udalM);
        }

        return (
            Aggregate("Dapper", 1, totalRuns, dapperTrials),
            Aggregate("UDAL", 1, totalRuns, udalTrials)
        );
    }

    public async Task<(AggregatedBenchmarkMetrics Dapper, AggregatedBenchmarkMetrics Udal)> RunPairedConcurrentBenchmarkAsync(
        int concurrency,
        int totalRuns,
        Action<int, int, BenchmarkMetrics, BenchmarkMetrics>? onProgress = null)
    {
        var dapperTrials = new List<BenchmarkMetrics>(totalRuns);
        var udalTrials = new List<BenchmarkMetrics>(totalRuns);

        for (int run = 1; run <= totalRuns; run++)
        {
            BenchmarkMetrics dapperM;
            BenchmarkMetrics udalM;

            if (run % 2 == 1)
            {
                dapperM = await RunConcurrentBenchmarkAsync("Dapper", concurrency);
                udalM = await RunConcurrentBenchmarkAsync("UDAL", concurrency);
            }
            else
            {
                udalM = await RunConcurrentBenchmarkAsync("UDAL", concurrency);
                dapperM = await RunConcurrentBenchmarkAsync("Dapper", concurrency);
            }

            dapperTrials.Add(dapperM);
            udalTrials.Add(udalM);

            onProgress?.Invoke(run, totalRuns, dapperM, udalM);
        }

        return (
            Aggregate("Dapper", concurrency, totalRuns, dapperTrials),
            Aggregate("UDAL", concurrency, totalRuns, udalTrials)
        );
    }

    private static AggregatedBenchmarkMetrics Aggregate(string name, int concurrency, int totalRuns, List<BenchmarkMetrics> trials)
    {
        var allAvgLatencies = trials.Select(t => t.AvgLatencyMs).OrderBy(x => x).ToList();
        int p95Index = Math.Min(allAvgLatencies.Count - 1, (int)Math.Floor(allAvgLatencies.Count * 0.95));

        return new AggregatedBenchmarkMetrics
        {
            Name = name,
            Concurrency = concurrency,
            TotalRuns = totalRuns,
            TotalQueriesPerRun = trials[0].TotalQueries,
            TotalRecordsPerRun = trials[0].TotalRecords,
            MeanTotalDurationMs = Math.Round(trials.Average(t => t.TotalDurationMs), 2),
            MeanAvgLatencyMs = Math.Round(trials.Average(t => t.AvgLatencyMs), 2),
            MinLatencyMs = Math.Round(trials.Min(t => t.MinLatencyMs), 2),
            MaxLatencyMs = Math.Round(trials.Max(t => t.MaxLatencyMs), 2),
            P95LatencyMs = Math.Round(allAvgLatencies[p95Index], 2),
            MeanThroughputRecordsPerSec = Math.Round(trials.Average(t => t.ThroughputRecordsPerSec), 0),
            MeanThroughputQueriesPerSec = Math.Round(trials.Average(t => t.ThroughputQueriesPerSec), 1),
            MeanGcGen0 = Math.Round(trials.Average(t => t.GcGen0), 1),
            MeanGcGen1 = Math.Round(trials.Average(t => t.GcGen1), 1),
            MeanGcGen2 = Math.Round(trials.Average(t => t.GcGen2), 1),
            MeanMemoryDeltaKb = Math.Round(trials.Average(t => t.MemoryDeltaKb), 0),
            MeanWorkingSetMb = Math.Round(trials.Average(t => t.WorkingSetMb), 1)
        };
    }

    private static void ForceGc()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
