using Grpc.Core;
using MicroServices.General.Service.NpOn.GeneralService.Mappers;
using MicroServices.General.Service.NpOn.IGeneralService;
using NpOn.Protos.General.V1;

namespace MicroServices.General.Service.NpOn.GeneralService.Services;

/// <summary>
/// Schema-First gRPC service endpoint generated from Protos/general/fld_master.proto.
/// Coexists with Code-First FldMasterService so consumers can migrate independently.
/// </summary>
public sealed class FldMasterProtoGrpcService(IFldMasterService fldMasterService)
    : FldMasterProtoService.FldMasterProtoServiceBase
{
    public override async Task<GetExecCommandReply> GetExecCommand(
        GetExecCommandRequest request,
        ServerCallContext context)
    {
        var command = FldMasterProtoMapper.ToExecutionCommand(request);
        var result = await fldMasterService.GetExecCommand(command);
        return FldMasterProtoMapper.ToExecCommandReply(result);
    }

    public override async Task<GetExecutionReply> GetExecution(
        GetExecCommandRequest request,
        ServerCallContext context)
    {
        var command = FldMasterProtoMapper.ToExecutionCommand(request);
        var result = await fldMasterService.GetExecution(command);
        return FldMasterProtoMapper.ToExecutionReply(result);
    }
}
