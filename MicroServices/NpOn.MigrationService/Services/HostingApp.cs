using MicroServices.Migration.Service.NpOn.IMigrationService;

namespace MicroServices.Migration.Service.NpOn.MigrationService.Services;

public class HostingApp(
    ICassandraMigration cassandraMigration,
    ILogger<HostingApp> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.MigrationService AppHostedService is starting multi-threaded");
        await cassandraMigration.TransferTable();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.MigrationService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}