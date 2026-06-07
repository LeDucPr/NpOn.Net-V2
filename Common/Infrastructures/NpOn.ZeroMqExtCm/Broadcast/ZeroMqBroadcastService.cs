using System.Collections.Concurrent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Broadcast;

public class ZeroMqBroadcastService : IZeroMqBroadcastService, IDisposable
{
    private readonly ILogger<ZeroMqBroadcastService> _logger;
    private NetMQContext? _context;
    private PublisherSocket? _publisherSocket;
    private SubscriberSocket? _subscriberSocket;
    private AsyncNetMQQueue<NetMQMessage>? _messageQueue;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;

    private readonly ConcurrentDictionary<string, Subject<string>> _topicSubjects = new();

    public ZeroMqBroadcastService(ILogger<ZeroMqBroadcastService> logger)
    {
        _logger = logger;
    }

    public void Start(string address)
    {
        _context = NetMQContext.Create();
        _publisherSocket = _context.CreatePublisherSocket();
        _subscriberSocket = _context.CreateSubscriberSocket();
        _messageQueue = new AsyncNetMQQueue<NetMQMessage>();
        _cancellationTokenSource = new CancellationTokenSource();

        _publisherSocket.Bind(address);
        _subscriberSocket.Connect(address);

        _receiveTask = Task.Run(async () => await ReceiveMessagesAsync(_cancellationTokenSource.Token));

        _logger.LogInformation($"ZeroMQ Broadcast Service started on {address}");
    }

    public void Stop()
    {
        _cancellationTokenSource?.Cancel();
        _receiveTask?.Wait();

        _publisherSocket?.Close();
        _publisherSocket?.Dispose();
        _publisherSocket = null;

        _subscriberSocket?.Close();
        _subscriberSocket?.Dispose();
        _subscriberSocket = null;

        _messageQueue?.Dispose();
        _messageQueue = null;

        _context?.Dispose();
        _context = null;

        _logger.LogInformation("ZeroMQ Broadcast Service stopped.");
    }

    public async Task PublishAsync(string topic, string message, CancellationToken cancellationToken = default)
    {
        if (_publisherSocket == null)
        {
            _logger.LogWarning("Publisher socket is not initialized. Cannot publish message.");
            return;
        }

        var msg = new NetMQMessage();
        msg.Append(topic);
        msg.Append(message);
        await _publisherSocket.SendMultipartMessageAsync(msg);
        _logger.LogDebug($"Published message to topic '{topic}': {message}");
    }

    public async Task SubscribeAsync(string topic, Action<string, string> handler, CancellationToken cancellationToken = default)
    {
        if (_subscriberSocket == null)
        {
            _logger.LogWarning("Subscriber socket is not initialized. Cannot subscribe.");
            return;
        }

        _subscriberSocket.Subscribe(topic);
        var subject = _topicSubjects.GetOrAdd(topic, _ => new Subject<string>());
        subject.Subscribe(msg => handler(topic, msg), cancellationToken);
        _logger.LogInformation($"Subscribed to topic: {topic}");
    }

    public void Unsubscribe(string topic)
    {
        if (_subscriberSocket == null)
        {
            _logger.LogWarning("Subscriber socket is not initialized. Cannot unsubscribe.");
            return;
        }

        _subscriberSocket.Unsubscribe(topic);
        if (_topicSubjects.TryRemove(topic, out var subject))
        {
            subject.Dispose();
        }
        _logger.LogInformation($"Unsubscribed from topic: {topic}");
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        if (_subscriberSocket == null || _messageQueue == null)
        {
            _logger.LogError("Subscriber socket or message queue is not initialized for receiving.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var msg = await _subscriberSocket.ReceiveMultipartMessageAsync(cancellationToken);
                if (msg.FrameCount >= 2)
                {
                    var topic = msg[0].ConvertToString();
                    var message = msg[1].ConvertToString();
                    if (_topicSubjects.TryGetValue(topic, out var subject))
                    {
                        subject.OnNext(message);
                    }
                }
            }
            catch (NetMQException ex)
            {
                if (ex.ErrorCode == NetMQError.ETERM)
                {
                    _logger.LogInformation("ZeroMQ context terminated. Stopping message reception.");
                    break;
                }
                _logger.LogError(ex, "Error receiving ZeroMQ message.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Message reception cancelled.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during ZeroMQ message reception.");
            }
        }
    }

    public void Dispose()
    {
        Stop();
        foreach (var subject in _topicSubjects.Values)
        {
            subject.Dispose();
        }
        _topicSubjects.Clear();
    }
}