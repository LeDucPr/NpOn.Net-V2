using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.NpOn.CommonApplication.Services;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using NpOn.PostgresDbFactory;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class AccountMenuPermissionService(
    IAccountMenuPermissionStorageAdapter accountMenuPermissionStorageAdapter,
    IPostgresFactoryWrapper baseRepository,
    ILogger<CommonService> logger
) : CommonService(logger), IAccountMenuPermissionService
{
    
}