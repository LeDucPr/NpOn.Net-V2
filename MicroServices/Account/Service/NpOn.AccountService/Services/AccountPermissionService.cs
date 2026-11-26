using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceDomain.Domains;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class AccountPermissionService(
    IAuthenticationStorageAdapter authenticationStorageAdapter,
    IAccountPermissionStorageAdapter accountPermissionStorageAdapter,
    IAccountTokenAndPermissionRedisRepository redisRepository,
    IAccountGroupStorageAdapter accountGroupStorageAdapter,
    IPostgresFactoryWrapper baseRepository,
    ILogger<AccountPermissionService> logger
) : CommonService(logger), IAccountPermissionService
{
    public static bool HasEffectiveBasePermission(
        long accountPermissionMask,
        EPermission controllerPermission,
        EPermission? requiredPermission = null)
    {
        if (accountPermissionMask == 0 || controllerPermission == EPermission.Unknown)
            return false;

        if ((accountPermissionMask & (long)controllerPermission) == 0)
            return false;

        if (requiredPermission.HasValue && requiredPermission.Value != EPermission.Unknown)
            return (accountPermissionMask & (long)requiredPermission.Value) != 0;

        return true;
    }

    public static long ToPermissionMask(AccountRModel? account) =>
        account?.Permission is { } permission ? (long)permission : 0L;

    public async Task<CommonResponse<AccountPermissionExceptionRModel[]?>>
        AccountPermissionExceptionQuickGetByAccountId(
            AccountPermissionExceptionGetByAccountIdQuery exceptionGetByAccountIdQuery)
    {
        return await CommonProcess<AccountPermissionExceptionRModel[]?>(async (response) =>
        {
            var (_, survivingExceptions, _) = await HealAndBuildPermissionStateAsync(
                exceptionGetByAccountIdQuery.AccountId,
                persistToRedis: true);
            response.Data = survivingExceptions.ToArray();
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<AccountPermissionExceptionCacheState?>> HealAndRefreshPermissionState(
        string accountId)
    {
        return await CommonProcess<AccountPermissionExceptionCacheState?>(async (response) =>
        {
            var (state, _, _) = await HealAndBuildPermissionStateAsync(accountId, persistToRedis: true);
            response.Data = state;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<AccountPermissionControllerRModel?>> GetPermissionControllerByCode(
        string controllerCode)
    {
        return await CommonProcess<AccountPermissionControllerRModel?>(async (response) =>
        {
            if (string.IsNullOrWhiteSpace(controllerCode))
            {
                response.Data = null;
                response.SetSuccess();
                return;
            }

            var controllers = await accountPermissionStorageAdapter.AccountPermissionControllerGetByCodes([controllerCode]);
            if (controllers == null)
            {
                response.Data = null;
                response.SetFail("Failed to load permission controller metadata");
                return;
            }

            response.Data = controllers.FirstOrDefault(x =>
                string.Equals(x.Code, controllerCode, StringComparison.Ordinal));
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> SyncPermissionWithController(
        AccountPermissionControllerAddOrChangeCommand[] command)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            if (command.Length == 0)
            {
                response.Data = true;
                response.SetSuccess();
                return;
            }

            List<AccountPermissionController> accountPermissions =
                command.Select(x => new AccountPermissionController(x))
                    .ToList();
            if (!(await baseRepository.Merge(accountPermissions))?.Status ?? false)
            {
                response.Data = false;
                response.SetFail("Sync AccountPermissionControllers fail");
                return;
            }

            string[] syncedCodes = command.Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray();
            await accountPermissionStorageAdapter.AccountPermissionExceptionsReconcileByControllerCodes(syncedCodes);
            await redisRepository.BumpPermissionCatalogVersion();

            response.Data = true;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> ClearOldControllersVersion(
        AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand command)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            if (!await accountPermissionStorageAdapter.AccountPermissionExceptionDeleteOldVersionByHostCode(
                    command.HostCode, command.VersionId.AsDefaultString()))
            {
                response.Data = false;
                response.SetFail("ClearOldControllersVersion fail");
                return;
            }

            await accountPermissionStorageAdapter.AccountPermissionExceptionDeleteOrphaned();
            await redisRepository.BumpPermissionCatalogVersion();

            response.Data = true;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> AddOrChangeAccountPermissionException(
        AccountPermissionExceptionAddOrChangeCommand[] commands)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            if (commands.Length == 0)
            {
                response.Data = true;
                response.SetSuccess();
                return;
            }

            if (commands.Select(x => x.AccountId).Distinct().ToArray() is { Length: > 1 })
            {
                response.SetFail("To many AccountId in Commands");
                return;
            }

            if (commands.Select(x => x.ControllerCode).Distinct().ToArray().Length != commands.Length)
            {
                response.SetFail("ControllerCode Duplicated in Commands");
                return;
            }

            string accountIdAsString = commands.First().AccountId.AsDefaultString();
            AccountPermissionException[] permissionExceptions =
                commands.Select(AccountPermissionException.FromCommands).ToArray();

            AccountRModel? accountTakePermission = await authenticationStorageAdapter.AccountGetById(accountIdAsString);
            if (accountTakePermission == null)
            {
                response.SetFail("Account not found");
                return;
            }

            long accountMask = ToPermissionMask(accountTakePermission);

            List<AccountPermissionControllerRModel>? rAccountPermissions =
                await accountPermissionStorageAdapter.AccountPermissionControllerGetByCodes(permissionExceptions
                    .Select(x => x.ControllerCode).ToArray());

            if (rAccountPermissions is not { Count: > 0 } ||
                rAccountPermissions.Count != permissionExceptions.Length)
            {
                response.SetFail("ControllerCode not found");
                return;
            }

            var controllerMap = rAccountPermissions.ToDictionary(x => x.Code, StringComparer.Ordinal);
            var (permissionExceptionsNeedChange, permissionExceptionsNeedDelete) =
                PartitionExceptionChanges(accountMask, permissionExceptions, controllerMap);

            if (permissionExceptionsNeedChange is { Count: > 0 })
                if (!(await baseRepository.Merge(permissionExceptionsNeedChange))?.Status ?? false)
                {
                    response.SetFail("Add/Change AccountPermissionExceptions fail");
                    return;
                }

            if (permissionExceptionsNeedDelete is { Count: > 0 })
                if (!(await baseRepository.Delete(permissionExceptionsNeedDelete))?.Status ?? false)
                {
                    response.SetFail("Delete AccountPermissionExceptions fail");
                    return;
                }

            var (healedState, _, _) = await HealAndBuildPermissionStateAsync(accountIdAsString, persistToRedis: true);
            if (healedState == null)
            {
                await redisRepository.DeleteCachingPermissionExceptionsByAccountId(accountIdAsString);
            }

            response.Data = true;
            response.SetSuccess();
        });
    }

    public async Task<CommonResponse<bool>> AddOrChangeManyAccountPermissionException(
        AccountPermissionExceptionAddOrChangeManyCommand command)
    {
        return await CommonProcess<bool>(async (response) =>
        {
            if (command.AccountIds is not { Length: > 0 } && command.GroupIds is not { Length: > 0 })
            {
                response.SetFail("AccountIds and GroupIds not null");
                return;
            }

            string[]? controllerCodes = command.ControllerComponents?
                .Select(x => x.ControllerCode).ToArray();
            List<AccountPermissionControllerRModel>? rAccountPermissions =
                await accountPermissionStorageAdapter.AccountPermissionControllerGetByCodes(controllerCodes);

            if (rAccountPermissions is not { Count: > 0 } ||
                rAccountPermissions.Count != controllerCodes?.Length)
            {
                response.SetFail("ControllerCode not found");
                return;
            }

            if (command.AccountIds is { Length: > 0 })
            {
                Guid[] directAccountIds = command.AccountIds.Distinct().ToArray();
                if (!await ApplyBatchPermissionExceptionsAsync(directAccountIds, command.ControllerComponents!, rAccountPermissions))
                {
                    response.SetFail("Add/Change AccountPermissionExceptions fail");
                    return;
                }
            }

            string[]? groupIdAsStrings = command.GroupIds?.Select(x => x.AsDefaultString()).ToArray();
            if (groupIdAsStrings is { Length: > 0 })
            {
                const int batchSize = 200;
                int pageIndex = 0;
                while (true)
                {
                    var accountGroups =
                        await accountGroupStorageAdapter.AccountGroupGetByGroupIds(
                            groupIdAsStrings, batchSize, pageIndex);
                    if (accountGroups is not { Count: > 0 })
                        break;

                    Guid[] groupMemberIds = accountGroups
                        .Where(x => x.Member != Guid.Empty)
                        .Select(x => x.Member.AsDefaultGuid())
                        .Distinct()
                        .ToArray();

                    if (groupMemberIds.Length > 0 &&
                        !await ApplyBatchPermissionExceptionsAsync(groupMemberIds, command.ControllerComponents!, rAccountPermissions))
                    {
                        response.SetFail("Add/Change AccountPermissionExceptions for group fail");
                        return;
                    }

                    pageIndex++;
                }
            }

            response.Data = true;
            response.SetSuccess();
        });
    }

    private async Task<bool> ApplyBatchPermissionExceptionsAsync(
        Guid[] accountIds,
        AccountPermissionExceptionAddOrChangeManyControllerCodeCommand[] controllerComponents,
        List<AccountPermissionControllerRModel> rAccountPermissions)
    {
        var permissionExceptions = accountIds
            .SelectMany(accountId => controllerComponents.Select(controllerComponent =>
                new AccountPermissionException
                {
                    AccountId = accountId,
                    ControllerCode = controllerComponent.ControllerCode,
                    AccessPermission = controllerComponent.AccessPermission,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                }))
            .ToArray();

        return await ChangePermissionAccount(accountIds, permissionExceptions, rAccountPermissions);
    }

    private async Task<bool> ChangePermissionAccount(Guid[]? accountIds,
        AccountPermissionException[] permissionExceptions,
        List<AccountPermissionControllerRModel> rAccountPermissions)
    {
        if (accountIds is not { Length: > 0 })
            return true;
        List<AccountRModel>? accountsTakePermission =
            await authenticationStorageAdapter.AccountGetByIds(accountIds.Select(x => x.ToString()).ToArray());
        if (accountsTakePermission is not { Count: > 0 })
            return true;

        var controllerMap = rAccountPermissions.ToDictionary(x => x.Code, StringComparer.Ordinal);
        List<string> accountIdAsStringNeedRefresh = [];
        List<AccountPermissionException> permissionExceptionsNeedChange = [];
        List<AccountPermissionException> permissionExceptionsNeedDelete = [];

        foreach (AccountRModel accountTakePermission in accountsTakePermission)
        {
            long accountMask = ToPermissionMask(accountTakePermission);
            var accountScopedExceptions = permissionExceptions
                .Where(x => x.AccountId == accountTakePermission.Id)
                .ToArray();
            var (changes, deletes) =
                PartitionExceptionChanges(accountMask, accountScopedExceptions, controllerMap);

            permissionExceptionsNeedChange.AddRange(changes);
            permissionExceptionsNeedDelete.AddRange(deletes);
            accountIdAsStringNeedRefresh.Add(accountTakePermission.Id.AsDefaultString());
        }

        if (permissionExceptionsNeedChange is { Count: > 0 })
            if (!(await baseRepository.Merge(permissionExceptionsNeedChange))?.Status ?? false)
                return false;

        if (permissionExceptionsNeedDelete is { Count: > 0 })
            if (!(await baseRepository.Delete(permissionExceptionsNeedDelete))?.Status ?? false)
                return false;

        return await redisRepository.DeleteCachingPermissionExceptionsByAccountIds(
            accountIdAsStringNeedRefresh.Distinct().ToArray());
    }

    private static (List<AccountPermissionException> NeedChange, List<AccountPermissionException> NeedDelete)
        PartitionExceptionChanges(
            long accountMask,
            IEnumerable<AccountPermissionException> exceptions,
            IReadOnlyDictionary<string, AccountPermissionControllerRModel> controllerMap)
    {
        List<AccountPermissionException> needChange = [];
        List<AccountPermissionException> needDelete = [];

        foreach (var ex in exceptions)
        {
            bool hasBaseAccess = controllerMap.TryGetValue(ex.ControllerCode, out var ctrl)
                                 && HasEffectiveBasePermission(accountMask, ctrl.Permission, ctrl.RequiredPermission);

            bool isValidOverride =
                (hasBaseAccess && ex.AccessPermission == EPermissionAccessController.Disable)
                || (!hasBaseAccess && ex.AccessPermission == EPermissionAccessController.Enable);

            if (isValidOverride)
                needChange.Add(ex);
            else
                needDelete.Add(ex);
        }

        return (needChange, needDelete);
    }

    private async Task<(
        AccountPermissionExceptionCacheState? State,
        List<AccountPermissionExceptionRModel> SurvivingExceptions,
        int HealedCount)> HealAndBuildPermissionStateAsync(string accountId, bool persistToRedis)
    {
        if (string.IsNullOrWhiteSpace(accountId))
            return (null, [], 0);

        AccountRModel? account = await authenticationStorageAdapter.AccountGetById(accountId);
        if (account == null)
        {
            if (persistToRedis)
                await redisRepository.DeleteCachingPermissionExceptionsByAccountId(accountId);
            return (new AccountPermissionExceptionCacheState
            {
                CatalogVersion = await redisRepository.GetPermissionCatalogVersion(),
                BasePermissionMask = 0,
                IsAccountActive = false,
                Exceptions = []
            }, [], 0);
        }

        long baseMask = ToPermissionMask(account);
        bool isActive = account.Status == EAccountStatus.Active;
        List<AccountPermissionExceptionRModel>? rawExceptions =
            await accountPermissionStorageAdapter.AccountPermissionExceptionGetByAccountId(accountId);

        List<AccountPermissionExceptionRModel> surviving = [];
        List<AccountPermissionException> obsoleteToDelete = [];

        if (rawExceptions is { Count: > 0 })
        {
            string[] codes = rawExceptions
                .Select(x => x.ControllerCode)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            List<AccountPermissionControllerRModel>? controllers = codes.Length > 0
                ? await accountPermissionStorageAdapter.AccountPermissionControllerGetByCodes(codes)
                : [];

            if (controllers == null)
            {
                // Transient controller lookup failure: preserve raw DB records and avoid poisoning Redis
                surviving = rawExceptions;
                long versionOnDegraded = await redisRepository.GetPermissionCatalogVersion();
                var degradedState = new AccountPermissionExceptionCacheState
                {
                    CatalogVersion = versionOnDegraded,
                    BasePermissionMask = baseMask,
                    IsAccountActive = isActive,
                    Exceptions = surviving.Select(x => new AccountPermissionExceptionCacheItem
                    {
                        ControllerCode = x.ControllerCode,
                        AccessPermission = x.AccessPermission
                    }).ToList()
                };
                return (degradedState, surviving, 0);
            }

            var controllerMap = controllers.ToDictionary(x => x.Code, StringComparer.Ordinal);
            foreach (var raw in rawExceptions)
            {
                if (!controllerMap.TryGetValue(raw.ControllerCode, out var ctrl))
                {
                    // Orphaned controller removed or renamed during deploy
                    obsoleteToDelete.Add(ToDomain(raw));
                    continue;
                }

                bool hasBaseAccess = HasEffectiveBasePermission(baseMask, ctrl.Permission, ctrl.RequiredPermission);
                bool isStillMeaningful =
                    (hasBaseAccess && raw.AccessPermission == EPermissionAccessController.Disable)
                    || (!hasBaseAccess && raw.AccessPermission == EPermissionAccessController.Enable);

                if (isStillMeaningful)
                    surviving.Add(raw);
                else
                    obsoleteToDelete.Add(ToDomain(raw));
            }

            if (obsoleteToDelete.Count > 0)
            {
                await baseRepository.Delete(obsoleteToDelete);
            }
        }

        long catalogVersion = await redisRepository.GetPermissionCatalogVersion();
        var cacheState = new AccountPermissionExceptionCacheState
        {
            CatalogVersion = catalogVersion,
            BasePermissionMask = baseMask,
            IsAccountActive = isActive,
            Exceptions = surviving.Select(x => new AccountPermissionExceptionCacheItem
            {
                ControllerCode = x.ControllerCode,
                AccessPermission = x.AccessPermission
            }).ToList()
        };

        if (persistToRedis)
            await redisRepository.SetAccountPermissionState(accountId, cacheState);

        return (cacheState, surviving, obsoleteToDelete.Count);
    }

    private static AccountPermissionException ToDomain(AccountPermissionExceptionRModel model) => new()
    {
        AccountId = model.AccountId,
        ControllerCode = model.ControllerCode,
        AccessPermission = model.AccessPermission,
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now
    };
}
