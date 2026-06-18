using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.TriggersAndMessages;
using NpOn.TrackerConstant.Broadcasts;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public class HostingApp(
    IRedisBroadcastFactoryWrapper redisBroadcastFactoryWrapper,
    // ITrackerLogService trackerLogService,
    IClickHouseFactoryWrapper clickHouseFactoryWrapper,
    ILogger<HostingApp> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.TrackerService App HostedService is starting multi-threaded");
        bool isStartDone = true;
        isStartDone = isStartDone & (await InitializeDatabaseTablesAsync(
            Path.Combine(AppContext.BaseDirectory, "SqlScripts", "init_tables_clickhouse.sql")));
        if (!isStartDone)
        {
            logger.LogCritical("NpOn.TrackerService App HostedService failed to initialize. Shutting down.");
            throw new Exception("Critical initialization failure.");
        }
    }

    private async Task<bool> RedisBroadcastMessageTrial()
    {
        var redisMsg = new RedisBroadcastMessage<WarningRedisBroadCastMessage>
        {
            Value = new WarningRedisBroadCastMessage
            {
                MessageLog = "NpOn.TrackerService is starting",
                ServiceName = "NpOn.TrackerService",
                TrackerLogLevel = ETrackerLogLevel.Debug,
                WarningDateUtc = DateTime.UtcNow,
            },
            Channel = WarningRedisBroadcastConstant.WarningRedisBroadcastChannel,
        };

        var publishActRs = await redisBroadcastFactoryWrapper.PublishAsync(redisMsg);
        bool isSuccess = publishActRs?.Status ?? false;
        if (isSuccess)
            logger.LogInformation("Successfully broadcasted start message via Redis Pub/Sub.");
        else
            logger.LogWarning("Failed to broadcast start message via Redis Pub/Sub.");
        return isSuccess;
    }

    private async Task<bool> InitializeDatabaseTablesAsync(string sqlFilePath)
    {
        if (!File.Exists(sqlFilePath))
        {
            logger.LogWarning("SQL initialization file not found at path: {Path}", sqlFilePath);
            return false;
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
        return true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("NpOn.TrackerService AppHostedService is stopping");
        return Task.CompletedTask;
    }
}