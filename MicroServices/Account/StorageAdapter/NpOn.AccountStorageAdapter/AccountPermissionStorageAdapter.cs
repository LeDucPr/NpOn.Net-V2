using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountPermissionStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterService fldMasterService
) : IAccountPermissionStorageAdapter
{
    public async Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountId(
        string accountId)
    {
        var execution =
            new TblFldExecutionCommand(AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountId);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("account_id", accountId)
            .ToCommand());
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<bool> AccountPermissionExceptionDeleteOldVersionByHostCode(string hostCode, string versionId)
    {
        var execution = new TblFldExecutionCommand(
            AccountPermissionServiceQueryCode.AccountExceptionControllersDeleteOldVersionByHostCode);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return false;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("host_code", hostCode)
            .AddParameterValue("version_id", versionId)
            .ToCommand());
        return result?.Status ?? false;
    }

    public async Task<List<AccountPermissionExceptionRModel>?>
        AccountPermissionExceptionGetByAccountIdAndControllerCodes(
            string accountId, string[] controllerCodes)
    {
        var execution = new TblFldExecutionCommand
            (AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountIdAndControllerCodes);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("account_id", accountId)
            .AddParameterValue("controller_codes", controllerCodes.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<List<AccountPermissionControllerRModel>?> AccountPermissionControllerGetByCodes(string[]? codes)
    {
        if (codes is not { Length: > 0 })
            return null;
        var execution = new TblFldExecutionCommand
            (AccountPermissionServiceQueryCode.AccountPermissionControllerGetByCodes);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("codes", codes.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountPermissionControllerRModel>();
    }
}