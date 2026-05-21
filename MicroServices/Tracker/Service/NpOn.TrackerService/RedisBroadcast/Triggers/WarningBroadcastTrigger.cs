using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.Messages;

namespace MicroServices.Tracker.Service.NpOn.TrackerService.RedisBroadcast.Triggers;

public class WarningBroadcastTrigger : BaseRedisBaseBroadcastTrigger<WarningRedisBroadCastMessage>
{
    public override string Channel => "WarningChannel";
}
