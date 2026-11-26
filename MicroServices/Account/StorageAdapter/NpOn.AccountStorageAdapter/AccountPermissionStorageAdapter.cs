using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using NpgsqlTypes;

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
        if (string.IsNullOrWhiteSpace(hostCode) || string.IsNullOrWhiteSpace(versionId))
            return false;

        try
        {
            var command = await generalCommandClient.GetExecCommandAsync(
                AccountPermissionServiceQueryCode.AccountExceptionControllersDeleteOldVersionByHostCode);
            if (command != null)
            {
                var result = await postgresFactoryWrapper.Execute(command
                    .AddParameterValue("host_code", hostCode)
                    .AddParameterValue("version_id", versionId));
                if (result?.Status == true)
                    return true;
            }
        }
        catch
        {
            // Fall through to parameterized SQL fallback when GeneralService lookup is unavailable/unseeded.
        }

        const string fallbackSql = """
                                   DELETE FROM acc_srv_account_permission_controller
                                   WHERE host_code = @host_code
                                     AND version_id <> @version_id;
                                   """;
        INpOnDbCommandParam versionParam = Guid.TryParse(versionId, out var parsedVersionId)
            ? new NpOnDbCommandParam<NpgsqlDbType>("version_id", parsedVersionId, NpgsqlDbType.Uuid)
            : new NpOnDbCommandParam("version_id", versionId);

        var fallbackResult = await postgresFactoryWrapper.ExecuteAsync(
            fallbackSql,
            [new NpOnDbCommandParam("host_code", hostCode), versionParam]);
        return fallbackResult?.Status ?? false;
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
        if (result is not { Status: true })
            return null;
        return result.ToList<AccountPermissionControllerRModel>() ?? [];
    }

    public async Task<bool> AccountPermissionExceptionDeleteOrphaned()
    {
        const string sql = """
                           DELETE FROM acc_srv_account_permission_exception e
                           WHERE NOT EXISTS (
                               SELECT 1 FROM acc_srv_account_permission_controller c
                               WHERE c.code = e.acc_srv_account_permission_controller_code
                           );
                           """;
        var result = await postgresFactoryWrapper.ExecuteAsync(sql, []);
        return result?.Status ?? false;
    }

    public async Task<bool> AccountPermissionExceptionsReconcileByControllerCodes(string[] controllerCodes)
    {
        if (controllerCodes is not { Length: > 0 })
            return true;

        const string sql = """
                           DELETE FROM acc_srv_account_permission_exception e
                           USING acc_srv_account a, acc_srv_account_permission_controller c
                           WHERE e.acc_srv_account_id = a.id
                             AND e.acc_srv_account_permission_controller_code = c.code
                             AND c.code = ANY(string_to_array(@controller_codes, ','))
                             AND (
                                 (
                                     (COALESCE(a.permission, 0) & c.permission) <> 0
                                     AND (c.required_permission IS NULL OR c.required_permission = 0 OR (COALESCE(a.permission, 0) & c.required_permission) <> 0)
                                     AND e.access_permission = @enable_access
                                 )
                                 OR
                                 (
                                     (
                                         (COALESCE(a.permission, 0) & c.permission) = 0
                                         OR (c.required_permission IS NOT NULL AND c.required_permission <> 0 AND (COALESCE(a.permission, 0) & c.required_permission) = 0)
                                     )
                                     AND e.access_permission = @disable_access
                                 )
                             );
                           """;
        List<INpOnDbCommandParam> parameters =
        [
            new NpOnDbCommandParam("controller_codes", controllerCodes.AsArrayJoin()),
            new NpOnDbCommandParam("enable_access", (int)EPermissionAccessController.Enable),
            new NpOnDbCommandParam("disable_access", (int)EPermissionAccessController.Disable),
        ];
        var result = await postgresFactoryWrapper.ExecuteAsync(sql, parameters);
        return result?.Status ?? false;
    }
}
