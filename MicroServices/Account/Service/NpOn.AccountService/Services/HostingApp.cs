using MicroServices.Account.Contracts.NpOn.AccountServiceContract.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceContract.Queries;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Service.NpOn.IAccountService;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class HostingApp(
    ILogger<HostingApp> logger,
    IAuthenticationService authenticationService
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is starting multi-threaded");
        await authenticationService.ChangeAccountStatus(new AccountSetStatusCommand
        {
            AccountId = new Guid("a13a55c8-230d-4e19-b795-1a113d196626"),
            AccountStatus = EAccountStatus.Active,
        });
        // await LoginStressTest();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is stopping");
        return Task.CompletedTask;
    }

    public async Task LoginStressTest()
    {
        int totalRequests = 500; // Tổng số lần login muốn test
        int maxParallelism = 100; 

        using var semaphore = new SemaphoreSlim(maxParallelism);

        int successCount = 0;
        int failCount = 0;
        int errorCount = 0;

        var tasks = Enumerable.Range(1, totalRequests).Select(async i =>
        {
            await semaphore.WaitAsync();
            try
            {
                var loginResponse = await authenticationService.Login(new AccountLoginQuery
                {
                    UserName = "KhaBanh",
                    Password = "jyGimGTAj2niJMgxijU7x7iR1RA=",
                    AuthType = EAuthentication.WebApp,
                    ClientId = "WEB_TEST_C"
                });

                if (loginResponse?.Data != null)
                {
                    Interlocked.Increment(ref successCount);
                    if (successCount % 50 == 0)
                        logger.LogInformation($"[Progress] Đã pass: {successCount}/{totalRequests}");
                }
                else
                {
                    Interlocked.Increment(ref failCount);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errorCount);
                // Lỗi hệ thống (như 10054) vẫn nên in ra để debug
                logger.LogInformation($"[Error] Request thứ {i} bị lỗi: {ex.Message}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        // Theo dõi thời gian thực hiện
        var watch = System.Diagnostics.Stopwatch.StartNew();

        await Task.WhenAll(tasks);

        watch.Stop();

        // Kết quả tổng thể
        logger.LogInformation("--------------------------------------------");
        logger.LogInformation($"HOÀN THÀNH STRESS TEST");
        logger.LogInformation($"Tổng thời gian: {watch.ElapsedMilliseconds} ms");
        logger.LogInformation($"Thành công: {successCount}");
        logger.LogInformation($"Thất bại (Data null): {failCount}");
        logger.LogInformation($"Lỗi hệ thống (Socket/gRPC): {errorCount}");
        logger.LogInformation($"Tốc độ trung bình: {totalRequests / watch.Elapsed.TotalSeconds:F2} req/s");
        logger.LogInformation("--------------------------------------------");
    }

    // public async Task LoginStressTest()
    // {
    //     int totalTasks = 100;
    //     int iterationsPerTask = 5;
    //
    //     // Tạo danh sách 10 Task
    //     var tasks = Enumerable.Range(1, totalTasks).Select(async taskId =>
    //     {
    //         for (int i = 1; i <= iterationsPerTask; i++)
    //         {
    //             try
    //             {
    //                 var loginResponse = await authenticationService.Login(new AccountLoginQuery
    //                 {
    //                     UserName = "KhaBanh",
    //                     Password = "jyGimGTAj2niJMgxijU7x7iR1RA=",
    //                     AuthType = EAuthentication.WebApp,
    //                     ClientId = "WEB_TEST_C"
    //                 });
    //
    //                 if (loginResponse.Data == null)
    //                 {
    //                     Console.WriteLine($"[Thread {taskId}] Chết tại lần thứ {i}");
    //                 }
    //                 else
    //                 {
    //                     // // Chỉ in ra mỗi 100 lần để tránh nghẽn Console (Console.WriteLine rất chậm)
    //                     // if (i % 100 == 0)
    //                     //     Console.WriteLine($"[Thread {taskId}] ---- Pass {i} -- {loginResponse.Data.FullName}");
    //                     Console.WriteLine($"[Thread {taskId}] ---- Pass {i} -- {loginResponse.Data.FullName}");
    //                 }
    //             }
    //             catch (Exception ex)
    //             {
    //                 Console.WriteLine($"[Thread {taskId}] Lỗi hệ thống: {ex.Message}");
    //             }
    //         }
    //     });
    //
    //     // Chạy tất cả cùng lúc
    //     await Task.WhenAll(tasks);
    //
    //     logger.LogInformation("Tất cả luồng đã hoàn thành.");
    // }
}