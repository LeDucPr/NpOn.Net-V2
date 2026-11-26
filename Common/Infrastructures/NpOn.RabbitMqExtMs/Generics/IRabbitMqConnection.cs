using Common.Extensions.NpOn.CommonEnums;
using RabbitMQ.Client;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;

public interface IRabbitMqConnection
{
    public IChannel Channel { get; }
    public string RoutingKey { get; }
    public string ExchangeName { get; }
    public bool IsConnected => Channel is { IsOpen: true };
    event Func<Task>? ReconnectedAsync;

    Task<bool> EnsureConnectedAsync() => Task.FromResult(IsConnected);

    Task<string> AddDefaultQueue(string exchangeName, string queueName,
        bool isCreateNewExchangeWhenExisted = false, bool isCreateNewQueueWhenExisted = false,
        string? topicRoutingKey = null, ERabbitMqExchangeType exchangeType = ERabbitMqExchangeType.Direct);

    Task AddQueue(RabbitMqQueueProperty property, bool isCreateNewExchangeWhenExisted = false,
        bool isCreateNewQueueWhenExisted = false);
}