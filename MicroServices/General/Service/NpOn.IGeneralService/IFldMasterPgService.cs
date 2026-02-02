using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Infrastructures.NpOn.CommonDb.DbResults.Grpc;
using MicroServices.General.Contract.GeneralServiceContract.ReadModels;
using MicroServices.General.Contract.NpOn.GeneralServiceContract.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceContract.Queries;

namespace MicroServices.General.Service.NpOn.IGeneralService;

[ServiceContract]
public interface IFldMasterPgService
{
    [OperationContract]
    Task<CommonResponse> ExecuteDomainAction(DomainActionCommand command);

    [OperationContract]
    Task<CommonResponse<List<TblFldRModel>>> GetExecution(TblFldExecution execution);

    [OperationContract]
    Task<CommonResponse<CommandRModel?>> GetExecCommand(TblFldExecution execution);

    [OperationContract]
    Task<CommonResponse<INpOnGrpcObject>> Execute(TblFldExecution execution);
}