using Grpc.Core;
using NpOn.Protos.Migration.V1;

namespace MicroServices.Migration.Service.NpOn.MigrationService.Services;

public sealed class CassandraMigrationProtoGrpcService(CassandraMigration cassandraMigration)
    : CassandraMigrationProtoService.CassandraMigrationProtoServiceBase
{
    public override async Task<MigrationCommonReplyProto> TransferTable(
        MigrationTransferTableRequestProto request,
        ServerCallContext context)
    {
        var result = await cassandraMigration.TransferTable();
        var reply = new MigrationCommonReplyProto
        {
            Status = result.Status,
            Version = result.Version,
            ServerTimeUnixMs = new DateTimeOffset(DateTime.SpecifyKind(result.ServerTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
        };
        if (result.ErrorCode.HasValue) reply.ErrorCode = (int)result.ErrorCode.Value;
        if (result.TotalRow.HasValue) reply.TotalRow = result.TotalRow.Value;
        if (result.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(result.ErrorMessages);
        return reply;
    }
}
