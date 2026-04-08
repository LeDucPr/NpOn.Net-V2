using System.ServiceModel;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;

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