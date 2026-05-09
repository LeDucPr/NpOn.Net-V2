using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
using NpOn.ITrackerStorageAdapter;

namespace NpOn.TrackerStorageAdapter;

public class SystemLogStorageAdapter(
    IClickHouseFactoryWrapper clickHouseFactoryWrapper
) : ISystemLogStorageAdapter
{
    public async Task<bool> InitializeSystemLogsTableAsync()
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS SystemLogs (
                Created_At DateTime64(3),
                -- Thêm cột Date để tối ưu Index theo ngày (giảm chi phí CPU khi lọc)
                EventDate Date MATERIALIZED toDate(Created_At), 
                Level LowCardinality(String),
                Log_Type Int16, -- Đổi Int2 thành Int16 (ClickHouse dùng Int8, 16, 32...)
                Source String,
                Message String,
                
                -- 1. Index phụ (Skipping Index) cho Log_Type
                INDEX idx_log_type Log_Type TYPE minmax GRANULARITY 3
            ) ENGINE = MergeTree()
            -- Partition theo tháng để quản lý file vật lý
            PARTITION BY toYYYYMM(Created_At)
            -- ORDER BY là Index chính: Sắp xếp để tìm nhanh theo Source và Ngày
            ORDER BY (Source, EventDate, Created_At)
            -- Thiết lập mặc định để truy vấn mới nhất lên đầu (cho các công cụ UI)
            SETTINGS index_granularity = 8192;";
        return (await clickHouseFactoryWrapper.Execute(new NpOnDbExecuteCommand
        {
            CommandText = sql,
            ExecType = EExecType.Query
        }))?.Status ?? false;
    }
}