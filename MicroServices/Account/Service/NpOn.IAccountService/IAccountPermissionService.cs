using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;

namespace MicroServices.Account.Service.NpOn.IAccountService;

[ServiceContract]
public interface IAccountPermissionService
{
    [OperationContract]
    Task<CommonResponse<AccountPermissionExceptionRModel[]?>> AccountPermissionExceptionQuickGetByAccountId(
        AccountPermissionExceptionGetByAccountIdQuery exceptionGetByAccountIdQuery);

    [OperationContract]
    Task<CommonResponse<bool>> SyncPermissionWithController(AccountPermissionControllerAddOrChangeCommand[] command);

    [OperationContract]
    Task<CommonResponse<bool>> ClearOldControllersVersion(
        AccountPermissionControllerDeleteByHostCodeAndVersionIdCommand command);

    [OperationContract]
    Task<CommonResponse<bool>> AddOrChangeAccountPermissionException(
        AccountPermissionExceptionAddOrChangeCommand[] commands);

    [OperationContract]
    Task<CommonResponse<bool>> AddOrChangeManyAccountPermissionException(
        AccountPermissionExceptionAddOrChangeManyCommand command);
}