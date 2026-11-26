using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountGroupStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IGeneralCommandClient generalCommandClient
) : IAccountGroupStorageAdapter
{
    public async Task<List<AccountGroupRModel>?> AccountGroupGetByGroupIds(
        string[] groupIds, int pageSize, int pageIndex) // Guids
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountGroupServiceCode.AccountGroupGetByGroupIds);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("group_ids", groupIds.AsArrayJoin())
            .AddParameterValue("page_size", pageSize)
            .AddParameterValue("page_index", pageIndex));
        return result.ToList<AccountGroupRModel>();
    }
}