using System.Collections.Concurrent;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Commands;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Results;
using Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;
using NetMQ;
using NetMQ.Sockets;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;

public class ZeroMqDriver : NpOnDbDriver
{
    private DealerSocket? _dealerSocket;
    private NetMQPoller? _poller;
    private NetMQQueue<NetMQMessage>? _sendQueue;

    protected readonly IObjectPool<ZeroMqResultSetWrapper>? ResultSetPool;

    public override string Name { get; set; } = "NpOn-V2.ZeroMqDriver";
    public override string Version { get; set; } = "1.0";

    public override bool IsValidSession => _dealerSocket != null && _dealerSocket.Options.Linger.TotalMilliseconds >= 0;

    private readonly ConcurrentDictionary<string, Func<string, Task<string>>> _callbacks = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<INpOnWrapperResult>> _pendingRequests = new();

    public ZeroMqDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            ResultSetPool = objectPoolStore.GetPool(() => new ZeroMqResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        await DisconnectAsync();

        _dealerSocket = new DealerSocket();
        _dealerSocket.Options.Identity = System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());

        _sendQueue = new NetMQQueue<NetMQMessage>();
        _poller = new NetMQPoller { _dealerSocket, _sendQueue };

        _dealerSocket.ReceiveReady += OnReceiveReady;
        _sendQueue.ReceiveReady += OnSendReady;

        int retryCount = 3;
        Exception? lastException = null;

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                if (Option.ConnectionString != null)
                    _dealerSocket.Connect(Option.ConnectionString);
                lastException = null;
                break;
            }
            catch (Exception ex)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i + 1));
                Console.WriteLine(
                    $"[ZeroMqDriver] Connection attempt {i + 1} failed. Retrying in {delay}. Error: {ex.Message}");
                await Task.Delay(delay, cancellationToken);
            }
        }

        if (lastException != null)
        {
            Console.WriteLine($"[ZeroMqDriver] Failed to connect to ZeroMQ after {retryCount} attempts.");
            await DisconnectAsync();
            throw lastException;
        }

        Version = "4.0.1.13";
        Name = _dealerSocket.Options.Identity.ToString() ?? "ZeroMQ";

        _poller.RunAsync();
    }

    private void OnReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        try
        {
            var msg = e.Socket.ReceiveMultipartMessage();
            if (msg.FrameCount > 0)
            {
                var json = msg.Last.ConvertToString();
                var zMessage = ZeroMqMessage.FromJson(json);

                if (zMessage.IsReply)
                {
                    if (_pendingRequests.TryRemove(zMessage.MessageId, out var tcs))
                    {
                        if (!string.IsNullOrEmpty(zMessage.ErrorMessage))
                        {
                            tcs.TrySetResult(CreateFailResult(new Exception(zMessage.ErrorMessage)));
                        }
                        else
                        {
                            var wrapper = ResultSetPool?.Get() ?? new ZeroMqResultSetWrapper();
                            if (ResultSetPool != null) wrapper.Reset();
                            wrapper.Init(zMessage.Payload ?? string.Empty);
                            if (ResultSetPool != null) wrapper.ReturnToPool = w => ResultSetPool.Return(w);
                            tcs.TrySetResult(wrapper);
                        }
                    }
                }
                else
                {
                    if (_callbacks.TryGetValue(zMessage.Channel, out var handler))
                    {
                        Task.Run(async () =>
                        {
                            try
                            {
                                var responsePayload = await handler(zMessage.Payload ?? string.Empty);
                                var replyMsg = new ZeroMqMessage
                                {
                                    MessageId = zMessage.MessageId,
                                    Channel = zMessage.Channel,
                                    Payload = responsePayload,
                                    IsReply = true
                                };

                                var netMsg = new NetMQMessage();
                                netMsg.Append(replyMsg.ToJson());
                                _sendQueue?.Enqueue(netMsg);
                            }
                            catch (Exception ex)
                            {
                                var errorMsg = new ZeroMqMessage
                                {
                                    MessageId = zMessage.MessageId,
                                    Channel = zMessage.Channel,
                                    IsReply = true,
                                    ErrorMessage = ex.Message
                                };
                                var netMsg = new NetMQMessage();
                                netMsg.Append(errorMsg.ToJson());
                                _sendQueue?.Enqueue(netMsg);
                            }
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZeroMqDriver] Error handling incoming ZeroMQ message: {ex.Message}");
        }
    }

    private void OnSendReady(object? sender, NetMQQueueEventArgs<NetMQMessage> e)
    {
        try
        {
            while (e.Queue.TryDequeue(out var msg, TimeSpan.Zero))
            {
                _dealerSocket?.SendMultipartMessage(msg);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZeroMqDriver] Error sending ZeroMQ message: {ex.Message}");
        }
    }

    public override async Task DisconnectAsync()
    {
        if (_poller != null)
        {
            _poller.StopAsync();
            _poller.Dispose();
            _poller = null;
        }

        if (_sendQueue != null)
        {
            _sendQueue.Dispose();
            _sendQueue = null;
        }

        if (_dealerSocket != null)
        {
            _dealerSocket.Close();
            _dealerSocket.Dispose();
            _dealerSocket = null;
        }

        foreach (var tcs in _pendingRequests.Values)
        {
            tcs.TrySetCanceled();
        }

        _pendingRequests.Clear();

        await Task.CompletedTask;
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ZeroMQ does not support transactions.");
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _sendQueue == null)
            return CreateFailResult(EDbError.Connection);

        if (command is ZeroMqCommand zmqCmd && zmqCmd.Callback != null)
        {
            _callbacks[zmqCmd.CommandText] = zmqCmd.Callback;
            return CreateSuccessResult();
        }

        int retryCount = 3;
        Exception? lastException = null;

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                return await ExecuteInternal(command);
            }
            catch (Exception ex) when (ex is NetMQException or TaskCanceledException or TimeoutException)
            {
                lastException = ex;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, i + 1));
                Console.WriteLine(
                    $"[ZeroMqDriver] Execute attempt {i + 1} failed. Retrying in {delay}. Error: {ex.Message}");
                await Task.Delay(delay);
            }
        }

        return CreateFailResult(lastException ?? new Exception("Unknown error during ZeroMQ execution"));
    }

    private async Task<INpOnWrapperResult> ExecuteInternal(IBaseNpOnDbCommand? command)
    {
        if (command is ZeroMqCommand zcmd)
        {
            var msgId = zcmd.MessageId ?? Guid.NewGuid().ToString();
            var zMessage = new ZeroMqMessage
            {
                MessageId = msgId,
                Channel = zcmd.CommandText,
                Payload = zcmd.Payload,
                IsReply = zcmd.IsReply
            };

            var netMsg = new NetMQMessage();
            netMsg.Append(zMessage.ToJson());

            if (zcmd.IsReply)
            {
                _sendQueue!.Enqueue(netMsg);
                return CreateSuccessResult();
            }
            else
            {
                var tcs = new TaskCompletionSource<INpOnWrapperResult>(TaskCreationOptions
                    .RunContinuationsAsynchronously);
                _pendingRequests[msgId] = tcs;

                _sendQueue!.Enqueue(netMsg);

                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    _pendingRequests.TryRemove(msgId, out _);
                    throw new TimeoutException("ZeroMQ request timed out");
                }

                return await tcs.Task;
            }
        }
        else
        {
            var netMsg = new NetMQMessage();
            netMsg.Append(command?.ToString() ?? string.Empty);
            _sendQueue!.Enqueue(netMsg);
            return CreateSuccessResult();
        }
    }

    public override async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ZeroMQ does not support transactions.");
    }

    #region private

    protected INpOnWrapperResult CreateFailResult(EDbError error)
    {
        var wrapper = ResultSetPool?.Get() ?? new ZeroMqResultSetWrapper();
        if (ResultSetPool != null) wrapper.Reset();
        wrapper.SetFail(error);
        if (ResultSetPool != null) wrapper.ReturnToPool = w => ResultSetPool.Return(w);
        return wrapper;
    }

    protected INpOnWrapperResult CreateFailResult(Exception ex)
    {
        var wrapper = ResultSetPool?.Get() ?? new ZeroMqResultSetWrapper();
        if (ResultSetPool != null) wrapper.Reset();
        wrapper.SetFail(ex);
        if (ResultSetPool != null) wrapper.ReturnToPool = w => ResultSetPool.Return(w);
        return wrapper;
    }

    protected INpOnWrapperResult CreateSuccessResult()
    {
        var wrapper = ResultSetPool?.Get() ?? new ZeroMqResultSetWrapper();
        if (ResultSetPool != null) wrapper.Reset();
        wrapper.Init("OK");
        wrapper.SetSuccess();
        if (ResultSetPool != null) wrapper.ReturnToPool = w => ResultSetPool.Return(w);
        return wrapper;
    }

    #endregion private
}