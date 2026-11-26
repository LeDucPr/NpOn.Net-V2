using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;

namespace MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

public interface IAccountTokenAndPermissionRedisRepository
{
    Task<bool> AddCachingPermissionException(string accountId,
        AccountPermissionExceptionRModel[]? accountPermissions);

    Task<string[]?> GetCachingPermissionException(string accountId);

    Task<bool> SetAccountPermissionState(string accountId, AccountPermissionExceptionCacheState state);
    Task<AccountPermissionExceptionCacheState?> GetAccountPermissionState(string accountId);
    Task<long> GetPermissionCatalogVersion();
    Task<long> BumpPermissionCatalogVersion();

    Task<bool> DeleteCachingPermissionExceptionsByAccountId(string accountId);
    Task<bool> DeleteCachingPermissionExceptionsByAccountIds(string[] accountIds);
}
