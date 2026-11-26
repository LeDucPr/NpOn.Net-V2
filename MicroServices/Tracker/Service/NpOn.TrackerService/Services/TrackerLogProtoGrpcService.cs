using Grpc.Core;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using MicroServices.Tracker.Service.NpOn.ITrackerService;
using NpOn.Protos.Tracker.V1;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public sealed class TrackerLogProtoGrpcService(ITrackerLogService trackerLogService)
    : TrackerLogProtoService.TrackerLogProtoServiceBase
{
    public override async Task<TrackerCommonReplyProto> PushLogs(
        PushTrackerLogsRequestProto request,
        ServerCallContext context)
    {
        var commands = request.Commands.Select(x => new TrackerLogAddCommand
        {
            EventDate = x.HasEventDateUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(x.EventDateUnixMs).UtcDateTime
                : null,
            Level = (ETrackerLogLevel)x.Level,
            Source = x.Source,
            Message = x.Message,
            TrackerLogTypes = x.TrackerLogTypes.Select(t => (ETrackerLogType)t).ToArray(),
            Attributes = new Dictionary<string, string>(x.Attributes)
        }).ToArray();

        var result = await trackerLogService.PushLogs(commands);
        var reply = new TrackerCommonReplyProto
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
