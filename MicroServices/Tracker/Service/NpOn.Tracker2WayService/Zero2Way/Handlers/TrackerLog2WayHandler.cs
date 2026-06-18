using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Messages;
using MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.TriggersAndMessages.Triggers;
using NpOn.TrackerConstant.ZeroMq2Ways;

namespace MicroServices.Tracker.Service.NpOn.Tracker2WayService.Zero2Way.Handlers;


public class TrackerLog2WayHandler
    : BaseZeroMqTwoWayHandler<TrackerTest2WayRequestCommand, TrackerTest2WayResponseCommand>
{
    public override string Channel => TrackerLog2WayConstant.TrackerLog2WayTest;

    public TrackerLog2WayHandler(TrackerLog2WayTrigger trigger) : base(trigger)
    {
    }
}