using System.Collections.Concurrent;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Events;
using Common.Infrastructures.NpOn.RabbitMqExtMs.Generics;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Common.Infrastructures.NpOn.RabbitMqExtMs.Receivers;

public abstract class RabbitMqConsumer<T> : RabbitMqComponent<T>, IRabbitMqConsumer<T>, IDisposable
    where T : CommonMessageContent
{
    private const int MaxConsumerRetries = 5;

    private readonly ILogger<RabbitMqConsumer<T>>? _logger;
    private readonly IRabbitMqConnection _rabbitMqConnection;
    private readonly ushort _prefetchCount;
    private ERabbitMqResponseType _responseType = ERabbitMqResponseType.BasicAck;
    protected Func<T, Task>? Handler;
    private readonly bool _autoAck;
    private readonly bool _isExternalConnection;
    protected readonly ERabbitMqExchangeType _exchangeType;
    protected readonly string? _routeKey;
    private readonly ConcurrentDictionary<string, int> _deliveryAttempts = new();
    private readonly CancellationTokenSource _consumerCts = new();
    private readonly SemaphoreSlim _consumeLock = new(1, 1);
    private readonly AsyncLocal<bool> _inUseDefaultFlow = new();
    private IChannel? _subscribedChannel;
    private int _recoveryLoopRunning;
    private bool _disposed;

    public ERabbitMqResponseType ResponseType
    {
        get => _responseType;
        set => _responseType = value;
    }

    public RabbitMqConsumer(IRabbitMqConnection rabbitMqConnection, ILogger<RabbitMqConsumer<T>>? logger = null,
        bool autoAck = true, ushort prefetchCount = 20,
        ERabbitMqExchangeType exchangeType = ERabbitMqExchangeType.Direct,
        string? routeKey = null)
    {
        _rabbitMqConnection = rabbitMqConnection;
        _isExternalConnection = true;
        _logger = logger;
        _autoAck = autoAck;
        _prefetchCount = prefetchCount;
        _exchangeType = exchangeType;
        _routeKey = routeKey;

        AddHandler();
        _rabbitMqConnection.ReconnectedAsync += OnConnectionReconnectedAsync;
        StartConsumeWithRecoveryAsync().GetAwaiter().GetResult();
    }

    private async Task StartConsumeWithRecoveryAsync()
    {
        try
        {
            await UseDefault().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "RabbitMqConsumer<{Type}> failed initial consume startup; background reconnect loop armed.",
                typeof(T).Name);
            EnsureRecoveryLoopStarted();
        }
    }

    private async Task OnConnectionReconnectedAsync()
    {
        if (_disposed || _consumerCts.IsCancellationRequested || _inUseDefaultFlow.Value)
            return;

        try
        {
            await UseDefault().ConfigureAwait(false);
            _logger?.LogInformation("RabbitMqConsumer<{Type}> re-subscribed after RabbitMQ connection recovery.",
                typeof(T).Name);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex,
                "RabbitMqConsumer<{Type}> re-subscribe after reconnect failed; arming recovery loop.",
                typeof(T).Name);
            EnsureRecoveryLoopStarted();
        }
    }

    private Task OnSubscribedChannelShutdownAsync(object sender, ShutdownEventArgs args)
    {
        if (ReferenceEquals(sender, _subscribedChannel))
        {
            _subscribedChannel = null;
        }

        if (!_disposed && !_consumerCts.IsCancellationRequested)
        {
            _logger?.LogWarning(
                "RabbitMqConsumer<{Type}> active channel closed ({ReplyText}); arming re-subscribe recovery loop.",
                typeof(T).Name, args.ReplyText);
            EnsureRecoveryLoopStarted();
        }

        return Task.CompletedTask;
    }

    private void EnsureRecoveryLoopStarted()
    {
        if (_disposed || _consumerCts.IsCancellationRequested)
            return;
        if (Interlocked.CompareExchange(ref _recoveryLoopRunning, 1, 0) != 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_consumerCts.IsCancellationRequested && !_disposed)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), _consumerCts.Token).ConfigureAwait(false);
                        await UseDefault().ConfigureAwait(false);
                        _logger?.LogInformation("RabbitMqConsumer<{Type}> recovered subscription.", typeof(T).Name);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch
                    {
                        // Retry on next backoff tick.
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _recoveryLoopRunning, 0);
            }
        }, _consumerCts.Token);
    }

    public async Task UseDefault(bool isDecompress = false)
    {
        if (!IsEnableType || _disposed)
            return;

        // Ensure connection OUTSIDE _consumeLock so ReconnectedAsync -> OnConnectionReconnectedAsync -> UseDefault()
        // never re-entrantly deadlocks on _consumeLock.
        await _rabbitMqConnection.EnsureConnectedAsync().ConfigureAwait(false);

        await _consumeLock.WaitAsync(_consumerCts.Token).ConfigureAwait(false);
        bool prevInUseDefault = _inUseDefaultFlow.Value;
        _inUseDefaultFlow.Value = true;
        try
        {
            if (_disposed)
                return;

            var routingKey = await _rabbitMqConnection.AddDefaultQueue(
                _rabbitMqConnection.ExchangeName,
                QueueName,
                isCreateNewQueueWhenExisted: true,
                topicRoutingKey: _routeKey,
                exchangeType: _exchangeType).ConfigureAwait(false);

            IChannel channel = _rabbitMqConnection.Channel
                ?? throw new InvalidOperationException("RabbitMQ channel unavailable.");

            if (ReferenceEquals(_subscribedChannel, channel) && channel.IsOpen)
                return;

            if (_subscribedChannel != null)
            {
                try
                {
                    _subscribedChannel.ChannelShutdownAsync -= OnSubscribedChannelShutdownAsync;
                }
                catch
                {
                    // Old AutorecoveringChannel may already be disposed by RabbitMqConnection.CreateConnection().
                }
                finally
                {
                    _subscribedChannel = null;
                }
            }

            channel.ChannelShutdownAsync += OnSubscribedChannelShutdownAsync;

            var consumer = new AsyncEventingBasicConsumer(channel);

            await channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _prefetchCount,
                global: false).ConfigureAwait(false);

            consumer.ReceivedAsync += async (_, ea) =>
            {
                byte[] body = ea.Body.ToArray();
                await ProcessDeliveryAsync(channel, ea, body, isDecompress).ConfigureAwait(false);
            };

            await channel.BasicConsumeAsync(
                queue: routingKey,
                autoAck: false,
                consumer: consumer).ConfigureAwait(false);

            _subscribedChannel = channel;
        }
        finally
        {
            _inUseDefaultFlow.Value = prevInUseDefault;
            _consumeLock.Release();
        }
    }

    private async Task ProcessDeliveryAsync(IChannel channel, BasicDeliverEventArgs ea, byte[] body, bool isDecompress)
    {
        RabbitMqEvent<T>? fullEvent;
        try
        {
            fullEvent = ProtoBufMode.ProtoBufDeserialize<RabbitMqEvent<T>>(body, isDecompress);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Deserialize error: {Message}", ex.Message);
            await SafeRejectAsync(channel, ea.DeliveryTag, requeue: false).ConfigureAwait(false);
            return;
        }

        if (Handler == null || fullEvent?.MessageContent == null)
        {
            _logger?.LogWarning("Invalid message (DeliveryTag: {DeliveryTag}) or no handler set. Rejecting.", ea.DeliveryTag);
            await SafeRejectAsync(channel, ea.DeliveryTag, requeue: false).ConfigureAwait(false);
            return;
        }

        string attemptKey = !string.IsNullOrEmpty(ea.BasicProperties?.MessageId)
            ? ea.BasicProperties.MessageId!
            : Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(body));

        try
        {
            await Handler(fullEvent.MessageContent).ConfigureAwait(false);
            _deliveryAttempts.TryRemove(attemptKey, out _);

            if (_autoAck)
            {
                await SafeAckAsync(channel, ea.DeliveryTag).ConfigureAwait(false);
            }
            else
            {
                switch (_responseType)
                {
                    case ERabbitMqResponseType.BasicAck:
                        await SafeAckAsync(channel, ea.DeliveryTag).ConfigureAwait(false);
                        break;
                    case ERabbitMqResponseType.BasicNack:
                        await SafeNackAsync(channel, ea.DeliveryTag, requeue: true).ConfigureAwait(false);
                        break;
                    case ERabbitMqResponseType.BasicReject:
                        await SafeRejectAsync(channel, ea.DeliveryTag, requeue: true).ConfigureAwait(false);
                        break;
                    default:
                        await SafeAckAsync(channel, ea.DeliveryTag).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            int attempts = _deliveryAttempts.AddOrUpdate(attemptKey, 1, (_, prev) => prev + 1);
            bool shouldRequeue = attempts < MaxConsumerRetries;
            _logger?.LogError(ex,
                "RabbitMqConsumer<{Type}> handler error (attempt {Attempt}/{MaxRetries}, requeue={Requeue}): {Message}",
                typeof(T).Name, attempts, MaxConsumerRetries, shouldRequeue, ex.Message);

            if (!shouldRequeue)
            {
                _deliveryAttempts.TryRemove(attemptKey, out _);
                await SafeRejectAsync(channel, ea.DeliveryTag, requeue: false).ConfigureAwait(false);
                return;
            }

            await SafeNackAsync(channel, ea.DeliveryTag, requeue: true).ConfigureAwait(false);
        }
    }

    private async Task SafeAckAsync(IChannel channel, ulong deliveryTag)
    {
        if (!channel.IsOpen) return;
        try { await channel.BasicAckAsync(deliveryTag, multiple: false).ConfigureAwait(false); }
        catch (Exception ex) { _logger?.LogDebug(ex, "SafeAckAsync skipped due to closed channel."); }
    }

    private async Task SafeNackAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        if (!channel.IsOpen) return;
        try { await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue).ConfigureAwait(false); }
        catch (Exception ex) { _logger?.LogDebug(ex, "SafeNackAsync skipped due to closed channel."); }
    }

    private async Task SafeRejectAsync(IChannel channel, ulong deliveryTag, bool requeue)
    {
        if (!channel.IsOpen) return;
        try { await channel.BasicRejectAsync(deliveryTag, requeue: requeue).ConfigureAwait(false); }
        catch (Exception ex) { _logger?.LogDebug(ex, "SafeRejectAsync skipped due to closed channel."); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rabbitMqConnection.ReconnectedAsync -= OnConnectionReconnectedAsync;
        if (_subscribedChannel != null)
        {
            try
            {
                _subscribedChannel.ChannelShutdownAsync -= OnSubscribedChannelShutdownAsync;
            }
            catch
            {
                // Ignore ObjectDisposedException if channel was already disposed.
            }
            finally
            {
                _subscribedChannel = null;
            }
        }
        _consumerCts.Cancel();
        _consumerCts.Dispose();
        _consumeLock.Dispose();
        if (!_isExternalConnection && _rabbitMqConnection is IDisposable disposableConnection)
        {
            disposableConnection.Dispose();
        }
    }

    public abstract void AddHandler();
}
