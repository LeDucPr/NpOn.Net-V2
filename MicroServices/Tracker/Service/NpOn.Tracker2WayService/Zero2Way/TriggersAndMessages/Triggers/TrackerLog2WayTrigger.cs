using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Messages;
// using Microsoft.Extensions.Logging;
using NpOn.TrackerConstant.ZeroMq2Ways;

namespace MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Triggers;

public class TrackerLog2WayTrigger
    : BaseZeroMqTwoWayTrigger<TrackerTest2WayRequestCommand, TrackerTest2WayResponseCommand>
{
    // private readonly ILogger<TrackerLog2WayTrigger> _logger;
    public TrackerLog2WayTrigger(/*ILogger<TrackerLog2WayTrigger> logger*/)
        : base(TrackerLog2WayConstant.TrackerLog2WayTest)
    {
        // _logger = logger;
    }

    protected override Task<TrackerTest2WayResponseCommand> ProcessLogicAsync(
        TrackerTest2WayRequestCommand requestCommand)
    {
        // _logger.LogInformation(
        //     $"[ZeroMQ TwoWay] Đã nhận và xử lý yêu cầu: {requestCommand.Action} lúc {requestCommand.Timestamp}");
        return Task.FromResult(new TrackerTest2WayResponseCommand
        {
            Message = $"Phản hồi từ TrackerService cho hành động '{requestCommand.Action}'",
            ProcessedAt = DateTime.UtcNow
        });
    }
}