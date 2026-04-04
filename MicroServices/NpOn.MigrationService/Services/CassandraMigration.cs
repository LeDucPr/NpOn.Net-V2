using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.CassandraFactory;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Migration.Service.NpOn.IMigrationService;

namespace MicroServices.Migration.Service.NpOn.MigrationService.Services;

public class CassandraMigration(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    ICassandraFactoryWrapper cassandraFactoryWrapper,
    ILogger<CommonService> logger
) : CommonService(logger), ICassandraMigration
{
    private readonly ILogger<CommonService> _logger = logger;
    private const string PostgresDefaultColumnName = "ctid";

    public async Task<CommonResponse> TransferTable()
    {
        return await CommonProcess(async response =>
        {
            string[] tableNames = [
                "acc_srv_account", 
                "acc_srv_account_address",
                "acc_srv_account_login",
            ];  
            int pageSize = 500;

            foreach (string tableName in tableNames)
            {
                int countTime = 1;
                // 1. Tìm cột để sắp xếp
                string sortColumn = await GetBestSortColumn(tableName);
                object? lastId = null;
                bool hasData = true;

                while (hasData)
                {
                    string whereClause = lastId == null ? "" : $"WHERE {sortColumn} > '{lastId.AsDefaultString()}'";
                    string query =
                        $"SELECT * FROM {tableName} {whereClause} ORDER BY {sortColumn} ASC LIMIT {pageSize}";

                    INpOnWrapperResult? result = await postgresFactoryWrapper.Execute(new NpOnDbExecuteCommand
                    {
                        CommandText = query,
                        ExecType = EExecType.Query
                    });

                    if (result is not INpOnTableWrapper tableWrapperGets ||
                        tableWrapperGets.RowWrappers is not { Count: > 0 } rowWrappersGets)
                    {
                        hasData = false;
                        continue;
                    }

                    lastId = tableWrapperGets.RowWrappers.Last().Value?.GetRowWrapper()[sortColumn].ValueAsObject
                        .AsDefaultString();

                    NpOnWrapperResultQueryBuilder cqlCommandBuilder =
                        result.ToQueryBuilder(EDbLanguage.Cql).WithTable(tableName);
                    string cqlCommandString = cqlCommandBuilder.Build(ERepositoryAction.Merge);

                    await cassandraFactoryWrapper.Execute(new NpOnDbExecuteCommand
                    {
                        CommandText = cqlCommandString,
                        ExecType = EExecType.Query
                    });
                    
                    _logger.LogInformation($"Sync Done {tableName} -- {pageSize*(countTime++)}");
                    // Nếu số lượng dòng lấy ra ít hơn pageSize tức là đã hết bảng
                    if (result is not INpOnTableWrapper tableWrapper ||
                        tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
                        hasData = false;
                }
                _logger.LogInformation($"Sync Done {tableName}");
            }

            response.SetSuccess();
        });
    }

    private async Task<string> GetBestSortColumn(string tableName)
    {
        // Query tìm PK, nếu không có thì tìm Index, cuối cùng là lấy cột đầu tiên
        string indexColumnKey = "column_name";
        string metaQuery = $@"
            SELECT {indexColumnKey} 
            FROM information_schema.key_column_usage 
            WHERE table_name = '{tableName}' AND constraint_name LIKE '%pkey%'
            UNION ALL
            SELECT column_name 
            FROM information_schema.columns 
            WHERE table_name = '{tableName}' 
            LIMIT 1";

        var result = await postgresFactoryWrapper.Execute(new NpOnDbExecuteCommand
        {
            CommandText = metaQuery,
            ExecType = EExecType.Query
        });

        if (result is not INpOnTableWrapper tableWrapper ||
            tableWrapper.RowWrappers is not { Count: > 0 } rowWrappers)
            return PostgresDefaultColumnName;
        string? indexKey = tableWrapper.RowWrappers.First().Value?.GetRowWrapper()[indexColumnKey].ValueAsObject
            .AsDefaultString();
        return
            indexKey ?? PostgresDefaultColumnName; // 'ctid' là cột vật lý mặc định của Postgres nếu bảng không có gì cả
    }
}