using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
// using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceCommand.Commands;
using MicroServices.Tracker.Contract.NpOn.TrackerServiceDomain.Domains;
using MicroServices.Tracker.Service.NpOn.ITrackerService;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.Services;

public class TrackerLogService(
    // IRedisBroadcastFactoryWrapper redisBroadcastFactoryWrapper,
    IClickHouseFactoryWrapper clickHouseFactoryWrapper,
    ILogger<CommonService> logger
) : CommonService(logger), ITrackerLogService
{
    public async Task<CommonResponse> PushLogs(TrackerLogAddCommand[]? commands)
    {
        return await CommonProcess(async (response) =>
        {
            TrackerLogMetrics.TrackPushLogsRequest(commands?.Length ?? 0);
            if (commands is not { Length : > 0 })
            {
                response.SetSuccess();
                return;
            }

            IEnumerable<SystemLog> systemLogs = commands.Select(x => new SystemLog(x));
            if (!(await clickHouseFactoryWrapper.Add(systemLogs))?.Status ?? false)
            {
                TrackerLogMetrics.TrackPushLogsFailed(commands.Length);
                response.SetFail("Push logs to ClickHouse failed");
                return;
            }

            TrackerLogMetrics.TrackPushLogsSuccess(commands.Length);
            response.SetSuccess();
        });
    }
}