using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Service.NpOn.IGeneralService;
using NpOn.PostgresDbFactory;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountMenuPermissionStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterPgService fldMasterPgService) : IAccountMenuPermissionStorageAdapter
{
}