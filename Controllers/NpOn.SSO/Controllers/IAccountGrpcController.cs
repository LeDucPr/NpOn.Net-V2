using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using Controllers.NpOn.SSO.Requests;
using Controllers.NpOn.SSO.OutputModels;

namespace Controllers.NpOn.SSO.Controllers;

[ServiceContract(Name = "Controllers.NpOn.SSO.Controllers.AccountController")]
public interface IAccountGrpcController
{
    [OperationContract]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    Task<CommonApiResponse<AccountLoginResponseWrapper>> Login(AccountLoginRequest request);
}
