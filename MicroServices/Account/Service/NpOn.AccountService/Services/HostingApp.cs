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
        
        var loginResponse = await authenticationService.Login(new AccountLoginQuery
        {
            UserName = "KhaBanh",
            Password = "GvN6GbQvBxyRiZ/oNsMW+Wwsa9o=",
            AuthType = EAuthentication.WebApp,
            ClientId = "WEB_TEST_C"
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
        
        int totalTasks = 100;
        int iterationsPerTask = 5;

        // Tạo danh sách 10 Task
        var tasks = Enumerable.Range(1, totalTasks).Select(async taskId =>
        {
            for (int i = 1; i <= iterationsPerTask; i++)
            {
                try
                {
                    var loginResponse = await authenticationService.Login(new AccountLoginQuery
                    {
                        UserName = "KhaBanh",
                        Password = "GvN6GbQvBxyRiZ/oNsMW+Wwsa9o=",
                        AuthType = EAuthentication.WebApp,
                        ClientId = "WEB_TEST_C"
                    });

                    if (loginResponse.Data == null)
                    {
                        Console.WriteLine($"[Thread {taskId}] Chết tại lần thứ {i}");
                    }
                    else
                    {
                        // // Chỉ in ra mỗi 100 lần để tránh nghẽn Console (Console.WriteLine rất chậm)
                        // if (i % 100 == 0)
                        //     Console.WriteLine($"[Thread {taskId}] ---- Pass {i} -- {loginResponse.Data.FullName}");
                        Console.WriteLine($"[Thread {taskId}] ---- Pass {i} -- {loginResponse.Data.FullName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Thread {taskId}] Lỗi hệ thống: {ex.Message}");
                }
            }
        });

        // Chạy tất cả cùng lúc
        await Task.WhenAll(tasks);

        logger.LogInformation("Tất cả luồng đã hoàn thành.");
    }
}