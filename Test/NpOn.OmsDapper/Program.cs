using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using NpOn.OmsDapper;

internal class Program
{
    const string connectionString = "Host=192.168.1.11;Port=5432;Username=postgres;Password=password;Database=account;";
    const int udalWrapperConnectionNumber = 200;
    const int totalConnectionNumber = 100;
    private static IPostgresFactoryWrapper udalWrapper;

    static async Task Main(string[] args)
    {
        IObjectPoolStore store = new ObjectPoolStore();
        store.PreAllocate(() => new NpOnWrapperResult(), 200);

        udalWrapper =
            new PostgresFactoryWrapper(
                connectionString,
                store,
                connectionNumber: udalWrapperConnectionNumber,
                isUseCaching: true);

        Console.WriteLine("=== NpOn.Net Performance Benchmark: Dapper vs Unified UDAL ===");

        // --- CHỌN BẬT/TẮT CÁC BÀI TEST TẠI ĐÂY (Ctrl + F5 để chạy) ---

        // 1. Chạy so sánh song song (Cũ)
        // await MultiTaskComparisonTest(10); 

        // 2. Chạy Độc lập Dapper (Để xem Dapper max load)
        // await MultiTaskDapperOnlyTest(20);

        // 3. Chạy Độc lập UDAL (Để xem UDAL max load - Không bị Dapper gây nghẽn)
        await MultiTaskUdalOnlyTest(20);

        // ------------------------------------------------------------

        Console.WriteLine("\nToàn bộ quá trình hoàn tất. Press any key to exit...");
        Console.ReadKey();
    }

    private static async Task MultiTaskComparisonTest(int numberOfTasks)
    {
        Console.WriteLine($"\n--- [COMPARISON] Đang khởi tạo {numberOfTasks} task chạy song song ---");
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                var runner = new BenchmarkRunner(connectionString, udalWrapper, taskId);
                await runner.Warmup();
                await runner.RunMappingBenchmark(true);
            }));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task MultiTaskDapperOnlyTest(int numberOfTasks)
    {
        Console.WriteLine($"\n--- [DAPPER ONLY] Đang khởi tạo {numberOfTasks} task chạy song song ---");
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                var runner = new BenchmarkRunner(connectionString, udalWrapper, taskId);
                await runner.RunDapperBenchmarkOnly(true);
            }));
        }

        await Task.WhenAll(tasks);
    }

    private static async Task MultiTaskUdalOnlyTest(int numberOfTasks)
    {
        Console.WriteLine($"\n--- [UDAL ONLY] Đang khởi tạo {numberOfTasks} task chạy song song ---");
        var tasks = new List<Task>();
        for (int i = 0; i < numberOfTasks; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                var runner = new BenchmarkRunner(connectionString, udalWrapper, taskId);
                await runner.RunUdalBenchmarkOnly(true);
            }));
        }

        await Task.WhenAll(tasks);
    }
}