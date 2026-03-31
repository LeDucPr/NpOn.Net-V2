using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Migration.Service.NpOn.IMigrationService;

namespace MicroServices.Migration.Service.NpOn.MigrationService.Services;

public class CassandraMigration(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    ILogger<CommonService> logger
) : CommonService(logger), ICassandraMigration
{
    public async Task<CommonResponse> TransferTable()
    {
        return await CommonProcess(async response =>
        {
            string[] tableNames =
            [
                "acc_srv_account"
            ];
            foreach (string tableName in tableNames)
            {
                string queryBuilder = $"select * from {tableName} limit 10";

                NpOnDbExecuteCommand queryCommand = new NpOnDbExecuteCommand
                {
                    CommandText = queryBuilder,
                    ExecType = EExecType.Query
                };
                var result = await postgresFactoryWrapper.Execute(queryCommand);
            }

            response.SetSuccess();
        });
    }
}