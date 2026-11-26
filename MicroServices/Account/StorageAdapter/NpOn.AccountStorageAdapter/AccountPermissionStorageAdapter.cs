using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountPermissionStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IGeneralCommandClient generalCommandClient
) : IAccountPermissionStorageAdapter
{
    public async Task<List<AccountPermissionExceptionRModel>?> AccountPermissionExceptionGetByAccountId(
        string accountId)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountId);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("account_id", accountId));
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<bool> AccountPermissionExceptionDeleteOldVersionByHostCode(string hostCode, string versionId)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountPermissionServiceQueryCode.AccountExceptionControllersDeleteOldVersionByHostCode);
        if (command == null)
            return false;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("host_code", hostCode)
            .AddParameterValue("version_id", versionId));
        return result?.Status ?? false;
    }

    public async Task<List<AccountPermissionExceptionRModel>?>
        AccountPermissionExceptionGetByAccountIdAndControllerCodes(
            string accountId, string[] controllerCodes)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountPermissionServiceQueryCode.AccountExceptionControllersGetByAccountIdAndControllerCodes);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("account_id", accountId)
            .AddParameterValue("controller_codes", controllerCodes.AsArrayJoin()));
        return result.ToList<AccountPermissionExceptionRModel>();
    }

    public async Task<List<AccountPermissionControllerRModel>?> AccountPermissionControllerGetByCodes(string[]? codes)
    {
        if (codes is not { Length: > 0 })
            return null;
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountPermissionServiceQueryCode.AccountPermissionControllerGetByCodes);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("codes", codes.AsArrayJoin()));
        return result.ToList<AccountPermissionControllerRModel>();
    }
}