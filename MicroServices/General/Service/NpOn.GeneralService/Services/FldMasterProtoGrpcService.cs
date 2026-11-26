using Grpc.Core;
using MicroServices.General.Service.NpOn.GeneralService.Mappers;
using MicroServices.General.Service.NpOn.IGeneralService;
using ProtoGeneral = NpOn.Protos.General.V1;

namespace MicroServices.General.Service.NpOn.GeneralService.Services;

/// <summary>
/// Schema-First gRPC service endpoint generated from Protos/general/fld_master.proto.
/// Coexists with Code-First FldMasterService so consumers can migrate independently.
/// </summary>
public sealed class FldMasterProtoGrpcService(IFldMasterService fldMasterService)
    : ProtoGeneral.FldMasterProtoService.FldMasterProtoServiceBase
{
    public override async Task<ProtoGeneral.CommandRModelResponseProto> GetExecCommand(
        ProtoGeneral.TblFldExecutionCommand request,
        ServerCallContext context)
    {
        var command = FldMasterProtoMapper.ToExecutionCommand(request);
        var result = await fldMasterService.GetExecCommand(command);
        return FldMasterProtoMapper.ToExecCommandReply(result);
    }

    public override async Task<ProtoGeneral.TblFldRModelListResponseProto> GetExecution(
        ProtoGeneral.TblFldExecutionCommand request,
        ServerCallContext context)
    {
        var command = FldMasterProtoMapper.ToExecutionCommand(request);
        var result = await fldMasterService.GetExecution(command);
        return FldMasterProtoMapper.ToExecutionReply(result);
    }

    public override async Task<ProtoGeneral.BulkSyncQueriesResponseProto> BulkSyncQueries(
        ProtoGeneral.BulkSyncQueriesCommand request,
        ServerCallContext context)
    {
        var command = FldMasterProtoMapper.ToBulkSyncQueriesCommand(request);
        var result = await fldMasterService.BulkSyncQueries(command);
        return FldMasterProtoMapper.ToBulkSyncQueriesResponse(result);
    }
}
