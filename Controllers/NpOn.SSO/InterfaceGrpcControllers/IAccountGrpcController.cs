using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using Controllers.NpOn.SSO.OutputModels;
using Controllers.NpOn.SSO.Requests;
using Microsoft.AspNetCore.Authorization;

namespace Controllers.NpOn.SSO.InterfaceGrpcControllers;

[ServiceContract]
public interface IAccountGrpcController
{
    [OperationContract]
    [AllowAnonymous]
    Task<CommonApiResponse<AccountLoginResponseWrapper>> Login(AccountLoginRequest request);
}