using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountMenuPermissionStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterPgService fldMasterPgService) : IAccountMenuPermissionStorageAdapter
{
}