using System;
using System.Threading.Tasks;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public abstract class BaseZeroMqTwoWayHandler
{
    public abstract string Channel { get; }
    public abstract Task<string> ParseAndTriggerAsync(string payload);
}

public abstract class BaseZeroMqTwoWayHandler<TRequest, TResponse> : BaseZeroMqTwoWayHandler
{
    public BaseZeroMqTwoWayTrigger<TRequest, TResponse> Trigger { get; }

    public override string Channel => Trigger.Channel;

    protected BaseZeroMqTwoWayHandler(BaseZeroMqTwoWayTrigger<TRequest, TResponse> trigger)
    {
        Trigger = trigger;
    }

    public override async Task<string> ParseAndTriggerAsync(string payload)
    {
        var msg = new ZeroMqMessage { Channel = Channel, Payload = payload };
        var response = await Trigger.TriggerAsync(msg);
        return Common.Extensions.NpOn.CommonMode.JsonModeWithCache.ToJson(response) ?? string.Empty;
    }
}
