using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.Tracker.Service.NpOn.ITrackerService;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;
using MicroServices.Tracker.Definitions.NpOn.TrackerEnum;
using MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.TriggersAndMessages;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.BroadcastHandlers;

public class WarningBroadcastHandler(
    IServiceProvider serviceProvider,
    WarningBroadcastTrigger trigger
) : BaseRedisBroadcastHandler<WarningRedisBroadCastMessage>(trigger,
    async (msg) =>
    {
        var warningMsg = msg as RedisBroadcastMessage<WarningRedisBroadCastMessage>;
        if (warningMsg?.Value == null)
            return false;

        using var scope = serviceProvider.CreateScope();
        var trackerLogService = scope.ServiceProvider.GetRequiredService<ITrackerLogService>();

        var command = new TrackerLogAddCommand
        {
            Message = warningMsg.Value.MessageLog ?? string.Empty,
            Source = warningMsg.Value.ServiceName ?? "Unknown",
            Level = warningMsg.Value.TrackerLogLevel,
            EventDate = warningMsg.Value.WarningDateUtc ?? DateTime.UtcNow,
            TrackerLogTypes = [ETrackerLogType.EventLog]
        };

        var response = await trackerLogService.PushLogs([command]);
        return response.Status;
    }
);