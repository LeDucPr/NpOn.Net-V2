using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public class HostingApp(
    ILogger<HostingApp> logger, 
    IClickHouseFactoryWrapper clickHouseFactoryWrapper
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.TrackerService AppHostedService is starting multi-threaded");
        
        await InitializeDatabaseTablesAsync();
    }

    private async Task InitializeDatabaseTablesAsync()
    {
        var sqlFilePath = Path.Combine(AppContext.BaseDirectory, "SqlScripts", "init_tables_clickhouse.sql");
        if (!File.Exists(sqlFilePath))
        {
            logger.LogWarning("SQL initialization file not found at path: {Path}", sqlFilePath);
            return;
        }

        var sqlContent = await File.ReadAllTextAsync(sqlFilePath);
        var commands = sqlContent.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var commandText in commands)
        {
            if (string.IsNullOrWhiteSpace(commandText)) continue;

            var result = await clickHouseFactoryWrapper.Execute(new NpOnDbExecuteCommand
            {
                CommandText = commandText,
                ExecType = EExecType.Query
            });

            if (result?.Status != true)
            {
                logger.LogError("Failed to execute initialization SQL command: {CommandText}", commandText);
                throw new Exception("Failed to initialize database tables.");
            }
        }
        
        logger.LogInformation("Database tables initialized successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.TrackerService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}