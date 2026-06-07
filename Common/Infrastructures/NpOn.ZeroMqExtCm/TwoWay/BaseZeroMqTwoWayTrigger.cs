using Common.Extensions.NpOn.CommonMode;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public abstract class BaseZeroMqTwoWayTrigger<TRequest, TResponse>
{
    public string Channel { get; }

    protected BaseZeroMqTwoWayTrigger(string channel)
    {
        Channel = channel;
    }

    public Task<TResponse> TriggerAsync(ZeroMqMessage requestMessage)
    {
        if (string.IsNullOrWhiteSpace(requestMessage.Channel))
            requestMessage.Channel = Channel;
        else if (requestMessage.Channel != Channel)
            throw new Exception("Channel not match");

        var requestData = JsonModeWithCache.FromJson<TRequest>(requestMessage.Payload ?? string.Empty);
        return ProcessLogicAsync(requestData!);
    }

    protected abstract Task<TResponse> ProcessLogicAsync(TRequest request);
}
