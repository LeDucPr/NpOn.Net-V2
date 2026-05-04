using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountInfoStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterService fldMasterService
) : IAccountInfoStorageAdapter
{
    // info
    public async Task<AccountInfoRModel?> AccountInfoActiveGetByAccountId(string accountId)
    {
        var execution = new TblFldExecutionCommand(AccountServiceQueryCode.AccountInfoActiveGetByAccountId);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("account_id", accountId)
            .ToCommand());
        return result.ToFirstOrDefault<AccountInfoRModel>();
    }

    public async Task<List<AccountInfoRModel>?> AccountInfoActiveGetByAccountIds(string[] accountIds)
    {
        var execution = new TblFldExecutionCommand(AccountServiceQueryCode.AccountInfoActiveGetByAccountIds);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("account_ids", accountIds.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountInfoRModel>();
    }


    // address
    public async Task<List<AccountAddressRModel>?> AccountAddressesGetByIds(string[] accountIds)
    {
        var execution = new TblFldExecutionCommand(AccountServiceQueryCode.AccountAddressesGetByIds);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("ids", accountIds.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountAddressRModel>();
    }

    public async Task<List<AccountAddressRModel>?> AccountAddressesDefaultGetByAccountIds(string[] accountIds)
    {
        var execution = new TblFldExecutionCommand(AccountServiceQueryCode.AccountAddressesDefaultGetByAccountIds);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("account_ids", accountIds.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountAddressRModel>();
    }
}