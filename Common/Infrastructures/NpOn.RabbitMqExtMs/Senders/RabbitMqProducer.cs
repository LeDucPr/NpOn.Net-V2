using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;

public class RabbitMqProducer(
    IRabbitMqConnection rabbitMqConnection,
    ILogger<RabbitMqProducer>? logger = null) : IRabbitMqProducer
{
    private static readonly IWrapperCacheStore<Type, (string QueueName, string RoutingKey)> ComponentCache =
        new WrapperCacheStore<Type, (string QueueName, string RoutingKey)>();

    private const int MaxPublishAttempts = 2;

    public void AddEvent(IRabbitMqEvent @event, bool isCompress = false)
    {
        FireAndForget(() => PublishAsync(@event, isCompress));
    }

    private async Task PublishAsync(IRabbitMqEvent @event, bool isCompress)
    {
        var eventType = @event.GetType();
        if (!eventType.IsGenericType ||
            eventType.GetGenericTypeDefinition() != typeof(RabbitMqEvent<>))
            return;

        var (queueName, routingKey) = ComponentCache.GetOrAdd(eventType, t =>
        {
            var messageContentType = t.GetGenericArguments()[0];
            var componentType = typeof(RabbitMqComponent<>).MakeGenericType(messageContentType);
            dynamic component = Activator.CreateInstance(componentType)!;
            return ((string)component.QueueName, (string)component.RoutingKey);
        });

        var body = ProtoBufMode.ProtoBufSerialize(@event, isCompress);
        var props = new BasicProperties { Persistent = true };
        string exchangeName = rabbitMqConnection.ExchangeName;

        for (int attempt = 1; attempt <= MaxPublishAttempts; attempt++)
        {
            try
            {
                if (rabbitMqConnection is RabbitMqConnection concrete && !concrete.IsConnected)
                {
                    await concrete.EnsureConnectedAsync().ConfigureAwait(false);
                }

                await rabbitMqConnection.AddDefaultQueue(exchangeName, queueName).ConfigureAwait(false);

                var channel = rabbitMqConnection.Channel;
                if (channel == null || !channel.IsOpen)
                {
                    throw new InvalidOperationException("RabbitMQ channel is not open.");
                }

                await channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: true,
                    basicProperties: props,
                    body: body).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < MaxPublishAttempts)
            {
                logger?.LogWarning(ex,
                    "RabbitMqProducer publish failed for queue '{QueueName}' on attempt {Attempt}, retrying once.",
                    queueName, attempt);
                await Task.Delay(200).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex,
                    "RabbitMqProducer publish failed permanently for queue '{QueueName}'. Event dropped cleanly without unhandled task crash.",
                    queueName);
                return;
            }
        }
    }

    private void FireAndForget(Func<Task> task)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await task().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "RabbitMqProducer background publish failure.");
            }
        });
    }
}
