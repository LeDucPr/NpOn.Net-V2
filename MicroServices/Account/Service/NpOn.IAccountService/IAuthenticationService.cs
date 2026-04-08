using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Events;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;

namespace MicroServices.Account.Service.NpOn.IAccountService;

[ServiceContract]
public interface IAuthenticationService
{
    [OperationContract]
    Task<CommonResponse<AccountLoginRModel>> Signup(AccountSignupCommand command);

    [OperationContract]
    Task<CommonResponse<bool>> ChangeAccountStatus(AccountSetStatusCommand command);

    [OperationContract]
    Task<CommonResponse<bool>> ChangeAccountPassword(AccountChangePasswordCommand command);

    [OperationContract]
    Task<CommonResponse<AccountLoginRModel>> Login(AccountLoginQuery query);

    [OperationContract]
    Task<CommonResponse<AccountLoginRModel>> RefreshToken(AccountRefreshTokenQuery query);

    [OperationContract]
    Task<CommonResponse<AccountLoginRModel>> GetLogonTokenBySessionId(AccountGetLogonInfoBySessionIdQuery query);

    [OperationContract]
    Task<CommonResponse<bool>> IsValidLogonPermissionExceptionControllers(
        AccountPermissionExceptionCachingCheckValidQuery query);

    [OperationContract]
    Task<CommonResponse<string>> LogOut(AccountLogoutQuery query);

    Task<CommonResponse> SaveLogin(AccountSaveLoginEvent @event);

    Task<CommonResponse> SaveLogout(AccountSaveLogoutEvent @event);
}