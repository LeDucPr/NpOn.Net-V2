using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using RabbitMQ.Client;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;

public class RabbitMqConnection : IRabbitMqConnection, IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private IConnection _connection;
    private IChannel _channel;
    private Dictionary<string, RabbitMqQueueProperty> _queueProperties;
    private string _routingKey = string.Empty;
    private string _exchangeName = string.Empty;
    private bool _disposed;

    public RabbitMqConnection(string connectString, string exchangeName)
    {
        _connectionFactory = new ConnectionFactory()
        {
            Uri = new Uri(connectString) // amqp://rabbitmq:password@localhost:5672/
        };
        CreateConnection().GetAwaiter().GetResult();
        _exchangeName = exchangeName;
        _queueProperties = new Dictionary<string, RabbitMqQueueProperty>();
    }

    private async Task CreateConnection()
    {
        _connection = await _connectionFactory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();
    }

    public IChannel Channel => _channel;
    public string RoutingKey => _routingKey;
    public string ExchangeName => _exchangeName;

    public async Task<string> AddDefaultQueue(string exchangeName, string queueName,
        bool isCreateNewExchangeWhenExisted = false, bool isCreateNewQueueWhenExisted = false,
        string? topicRoutingKey = null, ERabbitMqExchangeType exchangeType = ERabbitMqExchangeType.Direct)
    {
        RabbitMqQueueProperty newQueueProperty = new RabbitMqQueueProperty
        {
            ExchangeName = exchangeName,
            QueueName = queueName,
            ExchangeType = exchangeType
        };
        
        var actualQueueName = newQueueProperty.RoutingKey; // default deterministic queue name (e.g. ExchangeName.QueueName)
        var actualRoutingKey = string.IsNullOrEmpty(topicRoutingKey) ? actualQueueName : topicRoutingKey;
        
        _exchangeName = exchangeName;

        // Ensure we track the declare appropriately; queue can have multiple bindings but typically one exchange declare is enough
        var keys = new[] { exchangeName, queueName, actualRoutingKey }.Where(k => !string.IsNullOrEmpty(k));
        var dictKey = string.Join("_", keys);
        
        if (!_queueProperties.ContainsKey(dictKey))
        {
            bool recreateRequired = false;
            bool skipMainChannelDeclare = false;
            
            // Use a temporary channel to safely declare the exchange without risking the main channel
            try
            {
                using var tempChannel = await _connection.CreateChannelAsync();
                await tempChannel.ExchangeDeclareAsync(
                    exchange: newQueueProperty.ExchangeName,
                    type: newQueueProperty.ExchangeType.GetDisplayName(),
                    durable: newQueueProperty.Durable,
                    autoDelete: newQueueProperty.AutoDelete,
                    arguments: newQueueProperty.DictArgument);
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 406)
            {
                // 406 PRECONDITION_FAILED means the exchange exists but has a different type or parameters.
                if (isCreateNewExchangeWhenExisted)
                    recreateRequired = true;
                else
                    // Do not recreate. Accept the existing exchange type.
                    // Must skip main channel declaration to avoid crashing the main channel.
                    skipMainChannelDeclare = true;
            }

            if (recreateRequired)
            {
                await using var tempChannel = await _connection.CreateChannelAsync();
                await tempChannel.ExchangeDeleteAsync(newQueueProperty.ExchangeName);
                await tempChannel.ExchangeDeclareAsync(
                    exchange: newQueueProperty.ExchangeName,
                    type: newQueueProperty.ExchangeType.GetDisplayName(),
                    durable: newQueueProperty.Durable,
                    autoDelete: newQueueProperty.AutoDelete,
                    arguments: newQueueProperty.DictArgument);
            }

            if (!skipMainChannelDeclare)
            {
                // Declare on the main channel (idempotent, guaranteed to succeed now)
                await _channel.ExchangeDeclareAsync(
                    exchange: newQueueProperty.ExchangeName,
                    type: newQueueProperty.ExchangeType.GetDisplayName(),
                    durable: newQueueProperty.Durable,
                    autoDelete: newQueueProperty.AutoDelete,
                    arguments: newQueueProperty.DictArgument);
            }

            // Declare the queue
            await _channel.QueueDeclareAsync(queue: actualQueueName,
                durable: newQueueProperty.Durable,
                exclusive: newQueueProperty.Exclusive,
                autoDelete: newQueueProperty.AutoDelete,
                arguments: newQueueProperty.DictArgument);

            // Bind the queue to the exchange
            await _channel.QueueBindAsync(
                queue: actualQueueName,
                exchange: newQueueProperty.ExchangeName,
                routingKey: actualRoutingKey);

            _queueProperties.Add(dictKey, newQueueProperty);
        }

        return actualQueueName;
    }

    public async Task AddQueue(RabbitMqQueueProperty property, bool isCreateNewExchangeWhenExisted = false,
        bool isCreateNewQueueWhenExisted = false)
    {
        _exchangeName = property.ExchangeName;
        _routingKey = property.RoutingKey;
        if (_queueProperties.ContainsKey(_routingKey))
        {
            if (isCreateNewExchangeWhenExisted)
            {
                await _channel.ExchangeDeleteAsync(property.ExchangeName);
                await _channel.ExchangeDeclareAsync(
                    exchange: property.ExchangeName,
                    type: property.ExchangeType.GetDisplayName(),
                    durable: property.Durable,
                    autoDelete: property.AutoDelete,
                    arguments: property.DictArgument);
            }

            if (isCreateNewQueueWhenExisted)
            {
                await _channel.QueueDeclareAsync(queue: _routingKey,
                    durable: property.Durable,
                    exclusive: property.Exclusive,
                    autoDelete: property.AutoDelete,
                    arguments: property.DictArgument);
            }

            _queueProperties.Remove(_routingKey);
            _queueProperties.Add(_routingKey, property);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            try
            {
                _channel?.CloseAsync();
                _connection?.CloseAsync();
            }
            catch (Exception)
            {
                /* disposed */
            }
        }

        _disposed = true;
    }
}