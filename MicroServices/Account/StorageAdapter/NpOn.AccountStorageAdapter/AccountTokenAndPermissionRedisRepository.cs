using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountTokenAndPermissionRedisRepository
    : IAccountTokenAndPermissionRedisRepository
{
    private readonly IRedisFactoryWrapper _redisCachingFactoryWrapper;
    private readonly int _expirePermissionCacheMinutes;

    public AccountTokenAndPermissionRedisRepository(IRedisFactoryWrapper redisCachingFactoryWrapper)
    {
        _redisCachingFactoryWrapper = redisCachingFactoryWrapper;
        int expireTokenMinutes =
            EApplicationConfiguration.LoginTokenExpiresTime.GetAppSettingConfig().AsDefaultInt();
        int expireRefreshMinutes =
            EApplicationConfiguration.RefreshTokenExpiredTime.GetAppSettingConfig().AsDefaultInt();
        int baseMinutes = Math.Max(expireRefreshMinutes, expireTokenMinutes);
        _expirePermissionCacheMinutes = baseMinutes > 1 ? baseMinutes + 1 : 480;
    }

    #region Permission

    public async Task<bool> AddCachingPermissionException(string accountId,
        AccountPermissionExceptionRModel[]? accountPermissions)
    {
        var state = new AccountPermissionExceptionCacheState
        {
            CatalogVersion = await GetPermissionCatalogVersion(),
            BasePermissionMask = 0,
            IsAccountActive = true,
            Exceptions = accountPermissions == null
                ? []
                : accountPermissions.Select(x => new AccountPermissionExceptionCacheItem
                {
                    ControllerCode = x.ControllerCode,
                    AccessPermission = x.AccessPermission,
                }).ToList(),
        };

        return await SetAccountPermissionState(accountId, state);
    }

    public async Task<string[]?> GetCachingPermissionException(string accountId)
    {
        var state = await GetAccountPermissionState(accountId);
        return state?.Exceptions.Select(x => x.ControllerCode).ToArray();
    }

    public async Task<bool> SetAccountPermissionState(string accountId, AccountPermissionExceptionCacheState state)
    {
        try
        {
            string cacheKey = $"{AccountCachingCode.PrefixCachingAccountPermissionException}{accountId}";
            var res = await _redisCachingFactoryWrapper.SetAsBaseResult(
                cacheKey,
                JsonMode.ToJson(state),
                TimeSpan.FromMinutes(_expirePermissionCacheMinutes));
            return res?.Status ?? false;
        }
        catch
        {
            // Degrade gracefully when Redis/Dragonfly is offline; Postgres remains source of truth.
            return false;
        }
    }

    public async Task<AccountPermissionExceptionCacheState?> GetAccountPermissionState(string accountId)
    {
        try
        {
            var permissionExceptionCache =
                await _redisCachingFactoryWrapper.Get(
                    $"{AccountCachingCode.PrefixCachingAccountPermissionException}{accountId}");
            if (permissionExceptionCache == null || !permissionExceptionCache.Status)
                return null;

            var cacheValue = permissionExceptionCache.Result.Values.FirstOrDefault()?.ValueAsObject.AsEmptyString();
            if (string.IsNullOrEmpty(cacheValue)) return null;

            if (JsonMode.TryFromJson<AccountPermissionExceptionCacheState>(cacheValue, out var state) && state != null)
                return state;

            // Legacy string[] format lacks AccessPermission & BasePermissionMask -> force read-through self-heal
            return null;
        }
        catch
        {
            // Fail-fast to DB read-through when Redis/Dragonfly is unreachable.
            return null;
        }
    }

    public async Task<long> GetPermissionCatalogVersion()
    {
        try
        {
            var versionRes = await _redisCachingFactoryWrapper.Get(AccountCachingCode.PermissionCatalogVersionKey);
            if (versionRes == null || !versionRes.Status)
                return 0;

            var raw = versionRes.Result.Values.FirstOrDefault()?.ValueAsObject.AsEmptyString();
            return long.TryParse(raw, out var version) ? version : 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<long> BumpPermissionCatalogVersion()
    {
        try
        {
            long current = await GetPermissionCatalogVersion();
            long next = Math.Max(current + 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await _redisCachingFactoryWrapper.SetAsBaseResult(
                AccountCachingCode.PermissionCatalogVersionKey,
                next.ToString(),
                TimeSpan.FromDays(365));
            return next;
        }
        catch
        {
            return 0;
        }
    }

    public async Task<bool> DeleteCachingPermissionExceptionsByAccountId(string accountId)
    {
        try
        {
            string cacheKey = $"{AccountCachingCode.PrefixCachingAccountPermissionException}{accountId}";
            await _redisCachingFactoryWrapper.DeleteManyAsBaseResult(cacheKey);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteCachingPermissionExceptionsByAccountIds(string[] accountIds)
    {
        if (accountIds is not { Length: > 0 })
            return true;
        try
        {
            string[] cacheKeys = accountIds
                .Select(accountId => $"{AccountCachingCode.PrefixCachingAccountPermissionException}{accountId}").ToArray();
            await _redisCachingFactoryWrapper.DeleteManyAsBaseResult(cacheKeys);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion Permission
}
