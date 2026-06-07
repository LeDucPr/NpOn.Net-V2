namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Broadcast;

public interface IZeroMqBroadcastService
{
    Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default);
    Task SubscribeAsync(string topic, Action<string, string> handler, CancellationToken cancellationToken = default);
    void Unsubscribe(string topic);
    void Start(string address);
    void Stop();
}