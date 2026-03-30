namespace MicroServices.Migration.Service.NpOn.MigrationService.Services;

public class HostingApp(
    ILogger<HostingApp> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is starting multi-threaded");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.AccountService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}