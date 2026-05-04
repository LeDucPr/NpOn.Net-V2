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
    IFldMasterPgService fldMasterPgService
) : IAuthenticationStorageAdapter
{
    public async Task<List<AccountRModel>?> AccountGetByNumberPhoneOrEmailOrUsername(string phoneNumber,
        string email, string username)
    {
        var checkExistExecution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountGetByUsernameOrPhoneNumberOrEmail,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "phone_number",
                    StringValue = phoneNumber
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "email",
                    StringValue = email
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "username",
                    StringValue = username
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(checkExistExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountRModel?> AccountGetByUsernameAndPassword(string username, string password)
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountGetByUsernameAndPassword,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "username",
                    StringValue = username
                },
                new TblFldExecutionParamCommand
                {
                    ParamName = "password",
                    StringValue = password
                }
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountLoginRModel>?> AccountLoginInfoGetByRefreshToken(string refreshToken)
    {
        var execution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountLoginInfoGetByRefreshToken,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "refresh_token",
                    StringValue = refreshToken
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(execution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountLoginRModel>();
    }

    public async Task<AccountRModel?> AccountGetById(string accountId) // Guid
    {
        var accountExecution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountGetById,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "id",
                    StringValue = accountId,
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(accountExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToFirstOrDefault<AccountRModel>();
    }

    public async Task<List<AccountRModel>?> AccountGetByIds(string[]? accountIds) // Guids
    {
        var accountExecution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountGetByIds,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "ids",
                    StringValue = accountIds.AsArrayJoin(),
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(accountExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToList<AccountRModel>();
    }

    public async Task<AccountLoginRModel?> AccountLoginInfoGetBySessionId(string sessionId)
    {
        var logoutExecution = new TblFldExecutionCommand
        {
            Code = AuthenServiceQueryCode.AccountLoginInfoGetBySessionId,
            ExecParams =
            [
                new TblFldExecutionParamCommand
                {
                    ParamName = "session_id",
                    StringValue = sessionId
                },
            ]
        };
        var commandResponse = await fldMasterPgService.GetExecCommand(logoutExecution);
        if (!commandResponse.Status || commandResponse.Data == null)
            return null;
        var result = await postgresFactoryWrapper.Execute(commandResponse.Data.ToCommand());
        return result.ToFirstOrDefault<AccountLoginRModel>();
    }
}