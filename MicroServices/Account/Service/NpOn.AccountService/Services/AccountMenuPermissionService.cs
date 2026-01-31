using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using Common.Applications.NpOn.CommonApplication.Services;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class AccountMenuPermissionService(
    IAccountMenuPermissionStorageAdapter accountMenuPermissionStorageAdapter,
    INpOnPostgresFactoryWrapper baseRepository,
    ILogger<CommonService> logger
) : CommonService(logger), IAccountMenuPermissionService
{
    
}