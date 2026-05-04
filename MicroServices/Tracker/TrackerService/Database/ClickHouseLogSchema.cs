using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Database;

public static class ClickHouseLogSchema
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var factoryWrapper = serviceProvider.GetRequiredService<IClickHouseFactoryWrapper>();
        
        var sql = @"
            CREATE TABLE IF NOT EXISTS SystemLogs (
                Timestamp DateTime64(3),
                Level LowCardinality(String),
                Source String,
                Message String,
                Attributes Map(String, String)
            ) ENGINE = MergeTree()
            PARTITION BY toYYYYMM(Timestamp)
            ORDER BY (Source, Level, Timestamp);
        ";

        await factoryWrapper.Execute(new NpOnDbExecuteCommand
        {
            CommandText = sql,
            ExecType = EExecType.Query
        });
    }
}
