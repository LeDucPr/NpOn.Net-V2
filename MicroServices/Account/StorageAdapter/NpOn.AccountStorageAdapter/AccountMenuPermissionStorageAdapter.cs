using Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountMenuPermissionStorageAdapter(
    INpOnPostgresFactoryWrapper npOnPostgresFactoryWrapper,
    IFldMasterPgService fldMasterPgService) : IAccountMenuPermissionStorageAdapter
{
}