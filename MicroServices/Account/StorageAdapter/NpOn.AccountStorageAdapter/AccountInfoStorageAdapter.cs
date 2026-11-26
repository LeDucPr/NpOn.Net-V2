using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AccountInfoStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IGeneralCommandClient generalCommandClient
) : IAccountInfoStorageAdapter
{
    // info
    public async Task<AccountInfoRModel?> AccountInfoActiveGetByAccountId(string accountId)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountServiceQueryCode.AccountInfoActiveGetByAccountId);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("account_id", accountId));
        return result.ToFirstOrDefault<AccountInfoRModel>();
    }

    public async Task<List<AccountInfoRModel>?> AccountInfoActiveGetByAccountIds(string[] accountIds)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountServiceQueryCode.AccountInfoActiveGetByAccountIds);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("account_ids", accountIds.AsArrayJoin()));
        return result.ToList<AccountInfoRModel>();
    }


    // address
    public async Task<List<AccountAddressRModel>?> AccountAddressesGetByIds(string[] accountIds)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountServiceQueryCode.AccountAddressesGetByIds);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("ids", accountIds.AsArrayJoin()));
        return result.ToList<AccountAddressRModel>();
    }

    public async Task<List<AccountAddressRModel>?> AccountAddressesDefaultGetByAccountIds(string[] accountIds)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AccountServiceQueryCode.AccountAddressesDefaultGetByAccountIds);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("account_ids", accountIds.AsArrayJoin()));
        return result.ToList<AccountAddressRModel>();
    }
}