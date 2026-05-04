using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountGroupStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterService fldMasterService
) : IAccountGroupStorageAdapter
{
    public async Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(
        string[] groupIds, int pageSize, int pageIndex) // Guids
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AccountGroupServiceCode.AccountGroupGetByGroupIds,
        };
        CommonResponse<CommandRModel?> commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("group_ids", groupIds.AsArrayJoin())
            .AddParameterValue("page_size", pageSize)
            .AddParameterValue("page_index", pageIndex)
            .ToCommand());
        return result.ToList<AccountGroupRModel>();
    }
}