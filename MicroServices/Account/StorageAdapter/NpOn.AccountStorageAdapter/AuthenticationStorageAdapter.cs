using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountConstant;
using MicroServices.Account.StorageAdapter.NpOn.IAccountStorageAdapter;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Service.NpOn.IGeneralService;

namespace MicroServices.Account.StorageAdapter.NpOn.AccountStorageAdapter;

public class AuthenticationStorageAdapter(
    IPostgresFactoryWrapper postgresFactoryWrapper,
    IFldMasterService fldMasterService
) : IAuthenticationStorageAdapter
{
    public async Task<List<AccountRModel>?> AccountGetByNumberPhoneOrEmailOrUsername(string phoneNumber,
        string email, string username)
    {
        var checkExistExecution =
            new TblFldExecutionCommand(AuthenServiceQueryCode.AccountGetByUsernameOrPhoneNumberOrEmail);
        var commandResponse = await fldMasterService.GetExecCommand(checkExistExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("phone_number", phoneNumber)
            .AddParameterValue("email", email)
            .AddParameterValue("username", username)
            .ToCommand());
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountRModel?> AccountGetByUsernameAndPassword(string username, string password)
    {
        var execution = new TblFldExecutionCommand(AuthenServiceQueryCode.AccountGetByUsernameAndPassword);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("username", username)
            .AddParameterValue("password", password)
            .ToCommand());
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountLoginRModel>?> AccountLoginInfoGetByRefreshToken(string refreshToken)
    {
        var execution = new TblFldExecutionCommand(AuthenServiceQueryCode.AccountLoginInfoGetByRefreshToken);
        var commandResponse = await fldMasterService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("refresh_token", refreshToken)
            .ToCommand());
        return result.ToList<AccountLoginRModel>();
    }

    public async Task<AccountRModel?> AccountGetById(string accountId) // Guid
    {
        var accountExecution = new TblFldExecutionCommand(AuthenServiceQueryCode.AccountGetById);
        var commandResponse = await fldMasterService.GetExecCommand(accountExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("id", accountId)
            .ToCommand());
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountRModel>?> AccountGetByIds(string[]? accountIds) // Guids
    {
        var accountExecution = new TblFldExecutionCommand(AuthenServiceQueryCode.AccountGetByIds);
        var commandResponse = await fldMasterService.GetExecCommand(accountExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("ids", accountIds.AsArrayJoin())
            .ToCommand());
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountLoginRModel?> AccountLoginInfoGetBySessionId(string sessionId)
    {
        var logoutExecution = new TblFldExecutionCommand(AuthenServiceQueryCode.AccountLoginInfoGetBySessionId);
        var commandResponse = await fldMasterService.GetExecCommand(logoutExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data
            .AddParameterValue("session_id", sessionId)
            .ToCommand());
        return result.ToFirstOrDefault<AccountLoginRModel>();
    }
}