using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
using NpOn.ITrackerStorageAdapter;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public class HostingApp(
    ILogger<HostingApp> logger, 
    ISystemLogStorageAdapter systemLogStorageAdapter
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.MigrationService AppHostedService is starting multi-threaded");
        if (!await systemLogStorageAdapter.InitializeSystemLogsTableAsync())
            throw new Exception("Failed to initialize system logs table.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.MigrationService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}