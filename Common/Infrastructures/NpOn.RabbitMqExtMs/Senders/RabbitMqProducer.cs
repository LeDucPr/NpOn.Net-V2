using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using RabbitMQ.Client;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Senders;

public class RabbitMqProducer(IRabbitMqConnection rabbitMqConnection) : IRabbitMqProducer
{
    private static readonly IWrapperCacheStore<Type, (string QueueName, string RoutingKey)> ComponentCache = 
        new WrapperCacheStore<Type, (string QueueName, string RoutingKey)>();
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

        string exchangeName = rabbitMqConnection.ExchangeName;
        await rabbitMqConnection.AddDefaultQueue(exchangeName, queueName);

        var body = ProtoBufMode.ProtoBufSerialize(@event, isCompress);
        var props = new BasicProperties { Persistent = true };

        await rabbitMqConnection.Channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: true,
            basicProperties: props,
            body: body);
    }

    private void FireAndForget(Func<Task> task)
    {
        _ = Task.Run(async () => { await task(); });
    }
}