using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.KafkaExtCm.Configs;
using Common.Infrastructures.NpOn.KafkaExtCm.Events;
using Common.Infrastructures.NpOn.KafkaExtCm.Generics;
using Common.Infrastructures.NpOn.KafkaExtCm.Topics;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.KafkaExtCm.Receivers;

public abstract class KafkaConsumer<T> : KafkaComponent<T>, IKafkaConsumer<T>, IDisposable
    where T : CommonMessageContent
{
    private readonly ILogger<KafkaConsumer<T>>? _logger;
    private readonly IKafkaTopic _kafkaTopic;
    private IConsumer<string, byte[]>? _consumer;
    private CancellationTokenSource? _cts;
    private readonly bool _autoAck;
    private readonly ushort _prefetchCount; // for concurrency level
    private readonly ERabbitMqResponseType _responseType = ERabbitMqResponseType.BasicAck;
    private readonly Lock _commitLock = new Lock(); // ensure thread safety for Commit

    protected Func<T, Task>? Handler;

    protected KafkaConsumer(IKafkaTopic kafkaTopic, ILogger<KafkaConsumer<T>>? logger = null
        , bool autoAck = true, ushort prefetchCount = 20
    )
    {
        _kafkaTopic = kafkaTopic;
        _logger = logger;
        _autoAck = autoAck;
        _prefetchCount = prefetchCount;

        var addHandler = AddHandler;
        addHandler(); // same as RabbitMQ: set Handler before assigned consumer
        UseDefault().GetAwaiter().GetResult();
    }

    private async Task UseDefault(bool isDecompress = false)
    {
        if (!IsEnableType) return;

        var config = new ConsumerConfig(_kafkaTopic.GetKafkaConfig());

        foreach (var item in KafkaDefaultConfig.ConsumerConfigs.Values)
        {
            if (config.All(x => x.Key != item.Key))
            {
                config.Set(item.Key, item.DefaultValue);
            }
        }

        config.AutoOffsetReset = AutoOffsetReset.Latest;

        if (string.IsNullOrEmpty(config.GroupId))
            config.GroupId = $"{TopicName}.{PartitionName}"; // ??
            // config.GroupId = $"{TopicName}.{PartitionName}.{IndexerMode.CreateGuid()}"; // ??

        config.EnableAutoCommit = false; // We commit manually via SafeCommit

        _consumer = new ConsumerBuilder<string, byte[]>(config).Build();
        _consumer.Subscribe(TopicName);
        _cts = new CancellationTokenSource();
        _ = Task.Run(() => ConsumeLoop(isDecompress, _cts.Token));
        await Task.CompletedTask;
    }

    private async Task ConsumeLoop(bool isDecompress, CancellationToken token)
    {
        var semaphore = new SemaphoreSlim(_prefetchCount);
        try
        {
            while (!token.IsCancellationRequested)
            {
                await semaphore.WaitAsync(token); // Acquire permit

                ConsumeResult<string, byte[]>? cr = null;
                try
                {
                    cr = _consumer!.Consume(token);
                }
                catch
                {
                    // Restore permit if consume fails or gets cancelled
                    semaphore.Release();
                    throw;
                }

                if (cr == null)
                {
                    semaphore.Release();
                    continue;
                }

                // Fire and forget, passing the semaphore down to be released
                _ = ProcessMessageAsync(cr, isDecompress, semaphore);
            }
        }
        catch (OperationCanceledException)
        {
            // Stop loop gracefully
        }
        catch (Exception ex)
        {
            _logger?.LogError($"ConsumeLoop error: {ex.Message}");
        }
        finally
        {
            _consumer?.Close();
            
            // Gracefully wait until all active spawned tasks are fully completed
            for (int i = 0; i < _prefetchCount; i++)
            {
                await semaphore.WaitAsync(CancellationToken.None);
            }
            
            semaphore.Dispose();
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, byte[]> cr, bool isDecompress, SemaphoreSlim semaphore)
    {
        try
        {
            var fullEvent = ProtoBufMode.ProtoBufDeserialize<KafkaEvent<T>>(cr.Message.Value, isDecompress);
            if (fullEvent?.MessageContent != null && Handler != null)
            {
                await HandleMessage(cr, fullEvent.MessageContent);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error deserializing or processing message: {ex.Message}");
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandleMessage(ConsumeResult<string, byte[]> cr, T message)
    {
        try
        {
            await Handler!(message);

            if (_autoAck)
            {
                SafeCommit(cr);
            }
            else
            {
                CommitByResponseType(cr);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Handler error: {ex.Message}"); // Do not commit to allow the message to be reprocessed
        }
    }

    private void CommitByResponseType(ConsumeResult<string, byte[]> cr)
    {
        switch (_responseType)
        {
            case ERabbitMqResponseType.BasicAck:
                SafeCommit(cr);
                break;
            case ERabbitMqResponseType.BasicNack:
                // Kafka does not have nack, can Seek offset if reprocessing is desired
                break;
            case ERabbitMqResponseType.BasicReject:
                // Kafka does not have reject, can skip commit to reprocess
                break;
            case ERabbitMqResponseType.Default:
            default:
                // no commit
                break;
        }
    }

    private void SafeCommit(ConsumeResult<string, byte[]> cr)
    {
        lock (_commitLock)
        {
            try
            {
                _consumer!.Commit(cr);
            }
            catch (KafkaException e)
            {
                _logger?.LogError($"Commit error: {e.Error.Reason}");
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _consumer?.Dispose();
        _cts?.Dispose();
    }

    /// <summary>
    /// Similar to RabbitMQ: child class overrides to register message handler.
    /// </summary>
    public abstract void AddHandler();
}