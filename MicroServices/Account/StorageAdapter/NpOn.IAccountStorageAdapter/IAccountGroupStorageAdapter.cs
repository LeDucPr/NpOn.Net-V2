using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;

namespace MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

public interface IAccountGroupStorageAdapter
{
    Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(
        string[] groupIds, int pageSize, int pageIndex);
}