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
    IFldMasterPgService fldMasterPgService
) : IAccountPermissionStorageAdapter
{
    public async Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountId(
        string accountId)
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountId,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "account_id",
                    StringValue = accountId
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<bool> AccountPermissionExceptionDeleteOldVersionByHostCode(string hostCode, string versionId)
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AccountPermissionServiceQueryCode.AccountExceptionControllersDeleteOldVersionByHostCode,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "host_code",
                    StringValue = hostCode
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "version_id",
                    StringValue = versionId
                }
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return false;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result?.Status ?? false;
    }

    public async Task<List<AccountPermissionExceptionRModel>?>
        AccountPermissionExceptionGetByAccountIdAndControllerCodes(
            string accountId, string[] controllerCodes)
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountIdAndControllerCodes,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "account_id",
                    StringValue = accountId
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "controller_codes",
                    StringValue = controllerCodes.AsArrayJoin()
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<List<AccountPermissionControllerRModel>?> AccountPermissionControllerGetByCodes(string[]? codes)
    {
        if (codes is not { Length: > 0 })
            return null;
        var execution = new TblFldExecutionCommand
        {
            Code = AccountPermissionServiceQueryCode.AccountPermissionControllerGetByCodes,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "codes",
                    StringValue = codes.AsArrayJoin()
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountPermissionControllerRModel>();
    }
}