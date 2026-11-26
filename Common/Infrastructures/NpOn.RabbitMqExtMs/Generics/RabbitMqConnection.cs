using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonMode;
using RabbitMQ.Client;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;

public class RabbitMqConnection : IRabbitMqConnection, IDisposable
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly Dictionary<string, (RabbitMqQueueProperty Property, string RoutingKey, string ActualQueueName)> _registeredQueues = new();
    private string _routingKey = string.Empty;
    private string _exchangeName = string.Empty;
    private bool _disposed;

    public RabbitMqConnection(string connectString, string exchangeName)
    {
        _connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(connectString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
        };
        _exchangeName = exchangeName;

        // Do not crash service startup if RabbitMQ is temporarily down; EnsureConnectedAsync will heal lazily.
        try
        {
            CreateConnection().GetAwaiter().GetResult();
        }
        catch
        {
            // Deferred to lazy EnsureConnectedAsync.
        }
    }

    public event Func<Task>? ReconnectedAsync;

    public bool IsConnected => !_disposed && _connection is { IsOpen: true } && _channel is { IsOpen: true };

    private async Task CreateConnection()
    {
        if (_channel != null)
        {
            try { await _channel.DisposeAsync().ConfigureAwait(false); } catch { }
            _channel = null;
        }

        if (_connection != null)
        {
            try { await _connection.DisposeAsync().ConfigureAwait(false); } catch { }
            _connection = null;
        }

        _connection = await _connectionFactory.CreateConnectionAsync().ConfigureAwait(false);
        _channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
    }

    public async Task<bool> EnsureConnectedAsync()
    {
        if (IsConnected)
            return true;
        if (_disposed)
            return false;

        bool reconnected = false;
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsConnected)
                return true;
            if (_disposed)
                return false;

            await CreateConnection().ConfigureAwait(false);
            foreach (var entry in _registeredQueues.Values.ToArray())
            {
                await DeclareAndBindQueueInternalAsync(entry.Property, entry.ActualQueueName, entry.RoutingKey,
                    isCreateNewExchangeWhenExisted: false).ConfigureAwait(false);
            }

            reconnected = IsConnected;
        }
        catch
        {
            return false;
        }
        finally
        {
            _connectionLock.Release();
        }

        if (reconnected)
        {
            await NotifyReconnectedAsync().ConfigureAwait(false);
        }

        return reconnected;
    }

    private async Task NotifyReconnectedAsync()
    {
        var handlers = ReconnectedAsync?.GetInvocationList();
        if (handlers == null)
            return;

        foreach (Func<Task> handler in handlers)
        {
            try
            {
                await handler().ConfigureAwait(false);
            }
            catch
            {
                // Keep individual subscriber recovery errors from breaking sibling consumers.
            }
        }
    }

    public IChannel Channel => _channel!;
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

        var actualQueueName = newQueueProperty.RoutingKey;
        var actualRoutingKey = string.IsNullOrEmpty(topicRoutingKey) ? actualQueueName : topicRoutingKey;

        _exchangeName = exchangeName;

        var keys = new[] { exchangeName, queueName, actualRoutingKey }.Where(k => !string.IsNullOrEmpty(k));
        var dictKey = string.Join("_", keys);

        bool createdNewConnection = false;
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!IsConnected)
            {
                await CreateConnection().ConfigureAwait(false);
                createdNewConnection = IsConnected;
                if (createdNewConnection)
                {
                    foreach (var entry in _registeredQueues.Values.ToArray())
                    {
                        await DeclareAndBindQueueInternalAsync(entry.Property, entry.ActualQueueName, entry.RoutingKey,
                            isCreateNewExchangeWhenExisted: false).ConfigureAwait(false);
                    }
                }
            }

            if (!_registeredQueues.ContainsKey(dictKey) || isCreateNewQueueWhenExisted)
            {
                await DeclareAndBindQueueInternalAsync(
                    newQueueProperty, actualQueueName, actualRoutingKey, isCreateNewExchangeWhenExisted)
                    .ConfigureAwait(false);
                _registeredQueues[dictKey] = (newQueueProperty, actualRoutingKey, actualQueueName);
            }
        }
        finally
        {
            _connectionLock.Release();
        }

        if (createdNewConnection)
        {
            await NotifyReconnectedAsync().ConfigureAwait(false);
        }

        return actualQueueName;
    }

    private async Task DeclareAndBindQueueInternalAsync(
        RabbitMqQueueProperty newQueueProperty,
        string actualQueueName,
        string actualRoutingKey,
        bool isCreateNewExchangeWhenExisted)
    {
        if (_connection == null || _channel == null)
            throw new InvalidOperationException("RabbitMQ connection is not established.");

        bool recreateRequired = false;
        bool skipMainChannelDeclare = false;

        try
        {
            using var tempChannel = await _connection.CreateChannelAsync().ConfigureAwait(false);
            await tempChannel.ExchangeDeclareAsync(
                exchange: newQueueProperty.ExchangeName,
                type: newQueueProperty.ExchangeType.GetDisplayName(),
                durable: newQueueProperty.Durable,
                autoDelete: newQueueProperty.AutoDelete,
                arguments: newQueueProperty.DictArgument).ConfigureAwait(false);
        }
        catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 406)
        {
            if (isCreateNewExchangeWhenExisted)
                recreateRequired = true;
            else
                skipMainChannelDeclare = true;
        }

        if (recreateRequired)
        {
            await using var tempChannel = await _connection.CreateChannelAsync().ConfigureAwait(false);
            await tempChannel.ExchangeDeleteAsync(newQueueProperty.ExchangeName).ConfigureAwait(false);
            await tempChannel.ExchangeDeclareAsync(
                exchange: newQueueProperty.ExchangeName,
                type: newQueueProperty.ExchangeType.GetDisplayName(),
                durable: newQueueProperty.Durable,
                autoDelete: newQueueProperty.AutoDelete,
                arguments: newQueueProperty.DictArgument).ConfigureAwait(false);
        }

        if (!skipMainChannelDeclare)
        {
            await _channel.ExchangeDeclareAsync(
                exchange: newQueueProperty.ExchangeName,
                type: newQueueProperty.ExchangeType.GetDisplayName(),
                durable: newQueueProperty.Durable,
                autoDelete: newQueueProperty.AutoDelete,
                arguments: newQueueProperty.DictArgument).ConfigureAwait(false);
        }

        await _channel.QueueDeclareAsync(queue: actualQueueName,
            durable: newQueueProperty.Durable,
            exclusive: newQueueProperty.Exclusive,
            autoDelete: newQueueProperty.AutoDelete,
            arguments: newQueueProperty.DictArgument).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: actualQueueName,
            exchange: newQueueProperty.ExchangeName,
            routingKey: actualRoutingKey).ConfigureAwait(false);
    }

    public async Task AddQueue(RabbitMqQueueProperty property, bool isCreateNewExchangeWhenExisted = false,
        bool isCreateNewQueueWhenExisted = false)
    {
        _exchangeName = property.ExchangeName;
        _routingKey = property.RoutingKey;
        if (!IsConnected)
            await EnsureConnectedAsync().ConfigureAwait(false);
        if (_channel == null)
            return;

        if (_registeredQueues.ContainsKey(_routingKey))
        {
            if (isCreateNewExchangeWhenExisted)
            {
                await _channel.ExchangeDeleteAsync(property.ExchangeName).ConfigureAwait(false);
                await _channel.ExchangeDeclareAsync(
                    exchange: property.ExchangeName,
                    type: property.ExchangeType.GetDisplayName(),
                    durable: property.Durable,
                    autoDelete: property.AutoDelete,
                    arguments: property.DictArgument).ConfigureAwait(false);
            }

            if (isCreateNewQueueWhenExisted)
            {
                await _channel.QueueDeclareAsync(queue: _routingKey,
                    durable: property.Durable,
                    exclusive: property.Exclusive,
                    autoDelete: property.AutoDelete,
                    arguments: property.DictArgument).ConfigureAwait(false);
            }

            _registeredQueues[_routingKey] = (property, _routingKey, _routingKey);
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
        _disposed = true;
        if (disposing)
        {
            try
            {
                _channel?.CloseAsync().GetAwaiter().GetResult();
                _connection?.CloseAsync().GetAwaiter().GetResult();
                _channel?.Dispose();
                _connection?.Dispose();
            }
            catch
            {
                /* disposed */
            }
        }
    }
}
