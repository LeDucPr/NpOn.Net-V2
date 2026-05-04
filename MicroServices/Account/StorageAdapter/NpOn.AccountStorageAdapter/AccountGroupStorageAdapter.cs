using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountGroupStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterPgService fldMasterPgService
) : IAccountGroupStorageAdapter
{
    public async Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(
        string[] groupIds, int pageSize, int pageIndex) // Guids
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AccountGroupServiceCode.AccountGroupGetByGroupIds,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "group_ids",
                    StringValue = groupIds.AsArrayJoin()
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "page_size",
                    StringValue = pageSize.AsDefaultString()
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "page_index",
                    StringValue = pageIndex.AsDefaultString()
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountGroupRModel>();
    }
}