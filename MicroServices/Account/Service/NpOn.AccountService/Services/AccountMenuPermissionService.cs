using Common.Applications.NpOn.CommonApplication.Services;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Service.NpOn.IAccountService;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.Service.NpOn.AccountService.Services;

public class AccountMenuPermissionService(
    IAccountMenuPermissionStorageAdapter accountMenuPermissionStorageAdapter,
    IPostgresFactoryWrapper baseRepository,
    ILogger<CommonService> logger
) : CommonService(logger), IAccountMenuPermissionService
{
    
}