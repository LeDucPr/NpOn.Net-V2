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
    private readonly ILogger<RabbitMqConsumer<T>>? _logger;
    private readonly IRabbitMqConnection _rabbitMqConnection;
    private Type _typeT;
    private readonly ushort _prefetchCount; // for concurrency level
    private ERabbitMqResponseType _responseType = ERabbitMqResponseType.BasicAck;
    protected Func<T, Task>? Handler;
    private readonly bool _autoAck; // = true;
    private readonly bool _isExternalConnection;

    public ERabbitMqResponseType ResponseType
    {
        get => _responseType;
        set => _responseType = value;
    }

    public RabbitMqConsumer(IRabbitMqConnection rabbitMqConnection, ILogger<RabbitMqConsumer<T>>? logger = null,
        bool autoAck = true, ushort prefetchCount = 20
    ) // : base()
    {
        _rabbitMqConnection = rabbitMqConnection;
        _isExternalConnection = true; // Mark connection as external, do not dispose it
        _typeT = typeof(T);
        // _handler = handler;
        _logger = logger;
        _autoAck = autoAck;
        _prefetchCount = prefetchCount;


        // Call the abstract method to ensure the handler is set by the derived class *before* listening starts.
        AddHandler();
        UseDefault().GetAwaiter().GetResult();
    }

    // public RabbitMqConsumer(string connectionString, Func<T, Task> handler, bool autoAck = true) // : base()
    // {
    //     _rabbitMqConnection = new RabbitMqConnection(connectionString);
    //     _typeT = typeof(T);
    //     _handler = handler;
    //     _autoAck = autoAck;
    // }

    public async Task UseDefault(bool isDecompress = false)
    {
        if (!IsEnableType)
            return;
        var routingKey =
            await _rabbitMqConnection.AddDefaultQueue(_rabbitMqConnection.ExchangeName /* ?? ExchangeName*/, QueueName);
        IChannel channel = _rabbitMqConnection.Channel;
        var consumer = new AsyncEventingBasicConsumer(channel);

        // Set QoS (Quality of Service) to control how many messages are delivered at once.
        // Same as internal IEnumerabkle message
        // This is the key to enabling concurrent processing.
        await channel.BasicQosAsync(
            prefetchSize: 0, // No specific size limit
            prefetchCount: _prefetchCount, // Max number of unacknowledged messages
            global: false); // Apply per-consumer

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // BỎ HẲN _ = Task.Run(...) VÀ AWAIT TRỰC TIẾP
            byte[] body = ea.Body.ToArray();
            RabbitMqEvent<T>? fullEvent = null;
            
            try
            {
                fullEvent = ProtoBufMode.ProtoBufDeserialize<RabbitMqEvent<T>>(body, isDecompress);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Deserialize error: {ex.Message}");
                // Lỗi Format thì reject thẳng tay, không Requeue để tránh lặp vô tận
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
                return;
            }

            if (Handler == null || fullEvent?.MessageContent == null)
            {
                _logger?.LogWarning($"Invalid message (DeliveryTag: {ea.DeliveryTag}) or no handler set. Rejecting message.");
                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
                return;
            }

            try
            {
                // Await trực tiếp Handler, nếu Handler treo thì Timeout (cần handle trong Handler)
                await Handler(fullEvent.MessageContent);

                if (_autoAck)
                {
                    await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                }
                else
                {
                    switch (_responseType)
                    {
                        case ERabbitMqResponseType.BasicAck:
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                            break;
                        case ERabbitMqResponseType.BasicNack:
                            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
                            break;
                        case ERabbitMqResponseType.BasicReject:
                            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false);
                            break;
                        default:
                            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Handler error processing DeliveryTag {ea.DeliveryTag}: {ex.Message}");
                // Có lỗi nghiệp vụ thì Nack và đẩy lại vào Queue (requeue = true)
                // Lưu ý: Nếu lỗi do DB chết, requeue liên tục sẽ gây bão log. Nên có cơ chế Dead Letter Queue (DLX).
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: routingKey,
            // autoAck: _responseType == ERabbitMqResponseType.Default, // false when use switch - case {_responseType}
            autoAck: false, // Phải là false để logic manual ack/nack bên trên hoạt động
            consumer: consumer);
    }

    public void Dispose()
    {
        // Only dispose the connection if this class created it.
        // If the connection was passed in from the outside (Dependency Injection),
        // the outside code is responsible for its lifetime.
        if (!_isExternalConnection && _rabbitMqConnection is IDisposable disposableConnection)
        {
            disposableConnection.Dispose();
        }
    }

    public abstract void AddHandler();
}


// // 1. Tạo một cái Exchange riêng rành cho Rác (Dead Letter Exchange)
// await channel.ExchangeDeclareAsync("npon_dlx", ExchangeType.Direct, durable: true);
//
// // 2. Tạo cái Queue Rác (Dead Letter Queue) và map nó vào DLX
// await channel.QueueDeclareAsync("npon_dlq", durable: true, exclusive: false, autoDelete: false);
// await channel.QueueBindAsync("npon_dlq", "npon_dlx", "dlq_routing_key");
//
// // 3. Chỗ cấu hình Queue CHÍNH của ông, thêm cái Dictionary Arguments này vào:
// var queueArgs = new Dictionary<string, object>
// {
//     { "x-dead-letter-exchange", "npon_dlx" }, // Chỉ định tên DLX
//     { "x-dead-letter-routing-key", "dlq_routing_key" } // Chỉ định Routing Key của DLX
// };
//
// // Khai báo Queue chính kèm theo Args
// await channel.QueueDeclareAsync(
//     queue: QueueName, 
//     durable: true, 
//     exclusive: false, 
//     autoDelete: false, 
//     arguments: queueArgs); // <--- vip




// catch (Exception ex)
// {
//     _logger?.LogError($"Handler error processing DeliveryTag {ea.DeliveryTag}: {ex.Message}");
//                 
//     // QUAN TRỌNG: Đổi requeue = false. 
//     // Do mình đã cài đặt x-dead-letter-exchange ở Bước 1, 
//     // con Thỏ sẽ tự động chộp lấy cái message này và quăng sang npon_dlq thay vì vứt bỏ hoàn toàn.
//     await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false); 
// }