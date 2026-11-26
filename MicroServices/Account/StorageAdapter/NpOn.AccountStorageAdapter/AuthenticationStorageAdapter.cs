using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter.GeneralProtoClient;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AuthenticationStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IGeneralCommandClient generalCommandClient
) : IAuthenticationStorageAdapter
{
    public async Task<List<AccountRModel>?> AccountGetByNumberPhoneOrEmailOrUsername(string phoneNumber,
        string email, string username)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountGetByUsernameOrPhoneNumberOrEmail);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("phone_number", phoneNumber)
            .AddParameterValue("email", email)
            .AddParameterValue("username", username));
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountRModel?> AccountGetByUsernameAndPassword(string username, string password)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountGetByUsernameAndPassword);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("username", username)
            .AddParameterValue("password", password));
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountLoginRModel>?> AccountLoginInfoGetByRefreshToken(string refreshToken)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountLoginInfoGetByRefreshToken);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("refresh_token", refreshToken));
        return result.ToList<AccountLoginRModel>();
    }

    public async Task<AccountRModel?> AccountGetById(string accountId) // Guid
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountGetById);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("id", accountId));
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountRModel>?> AccountGetByIds(string[]? accountIds) // Guids
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountGetByIds);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("ids", accountIds.AsArrayJoin()));
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountLoginRModel?> AccountLoginInfoGetBySessionId(string sessionId)
    {
        var command = await generalCommandClient.GetExecCommandAsync(
            AuthenServiceQueryCode.AccountLoginInfoGetBySessionId);
        if (command == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(command
            .AddParameterValue("session_id", sessionId));
        return result.ToFirstOrDefault<AccountLoginRModel>();
    }
}