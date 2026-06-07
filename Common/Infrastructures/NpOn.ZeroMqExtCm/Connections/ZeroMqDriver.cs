using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbTransactions;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.ZeroMqExtCm.Results;
using NetMQ;
using NetMQ.AsyncIO;
using NetMQ.Sockets;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Connections;

public class ZeroMqDriver : NpOnDbDriver
{
    private NetMQContext? _context;
    private DealerSocket? _dealerSocket;
    private ResponseSocket? _responseSocket;
    private Polly.Retry.RetryPolicy? _retryPolicy;

    protected readonly IObjectPool<ZeroMqResultSetWrapper>? ResultSetPool;

    public override string Name { get; set; } = "NpOn-V2.ZeroMqDriver";
    public override string Version { get; set; } = "1.0";

    public override bool IsValidSession => _dealerSocket != null && _dealerSocket.Options.Linger.TotalMilliseconds >= 0;

    public ZeroMqDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            ResultSetPool = objectPoolStore.GetPool(() => new ZeroMqResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return; // Already connected.
        }

        await DisconnectAsync();

        _context = NetMQContext.Create();

        // Determine socket type based on connection string or configuration
        // For simplicity, assuming DealerSocket for now. This might need refinement.
        _dealerSocket = _context.CreateDealerSocket();

        // Configure retry policy for connection attempts
        _retryPolicy = Polly.Policy.Handle<NetMQException>()
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, context) =>
                {
                    Logger.LogError(exception, $"ZeroMQ connection attempt {retryCount} failed. Retrying in {timeSpan}.");
                });

        try
        {
            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _dealerSocket.ConnectAsync(Option.ConnectionString);
                // You might want to send a handshake message here to confirm connection
            });
            Version = "4.0.1.13"; // NetMQ version
            Name = _dealerSocket.Options.Identity.ToString() ?? "ZeroMQ";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to connect to ZeroMQ.");
            await DisconnectAsync();
            throw;
        }
    }

    public override async Task DisconnectAsync()
    {
        if (_dealerSocket != null)
        {
            _dealerSocket.Close();
            _dealerSocket.Dispose();
            _dealerSocket = null;
        }
        if (_context != null)
        {
            _context.Dispose();
            _context = null;
        }
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        // ZeroMQ does not natively support transactions in the same way as traditional databases.
        // This method might need to be adapted or throw an exception if transactions are not applicable.
        throw new NotSupportedException("ZeroMQ does not support transactions.");
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _dealerSocket == null)
        {
            return CreateFailResult(EDbError.Connection);
        }

        try
        {
            // Serialize command to a format suitable for ZeroMQ (e.g., JSON, Protobuf)
            // For simplicity, using string representation here.
            string commandString = command?.ToString() ?? string.Empty;

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _dealerSocket.SendFrameAsync(commandString);
                var replyFrame = await _dealerSocket.ReceiveFrameStringAsync();

                // Deserialize the reply
                // This part requires a defined contract for request/response messages.
                // For now, assuming a simple string reply.
                if (ResultSetPool != null)
                {
                    var wrapper = ResultSetPool.Get();
                    wrapper.Reset();
                    wrapper.Init(replyFrame); // Assuming Init can handle string
                    wrapper.ReturnToPool = w => ResultSetPool.Return(w);
                    return wrapper;
                }
                else
                {
                    return new ZeroMqResultSetWrapper().Init(replyFrame);
                }
            });
        }
        catch (Exception ex)
        {
            return CreateFailResult(ex);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    public override async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        // ZeroMQ does not support transactions in the traditional sense.
        // This method should be adapted or throw an exception.
        throw new NotSupportedException("ZeroMQ does not support transactions.");
    }

    #region private

    protected INpOnWrapperResult CreateFailResult(EDbError error)
    {
        if (ResultSetPool != null)
        {
            var wrapper = ResultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(error);
            wrapper.ReturnToPool = w => ResultSetPool.Return(w);
            return wrapper;
        }

        return new ZeroMqResultSetWrapper().SetFail(error);
    }

    protected INpOnWrapperResult CreateFailResult(Exception ex)
    {
        if (ResultSetPool != null)
        {
            var wrapper = ResultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(ex);
            wrapper.ReturnToPool = w => ResultSetPool.Return(w);
            return wrapper;
        }

        return new ZeroMqResultSetWrapper().SetFail(ex);
    }

    private void ResetSessionTimeout()
    {
        // Implement session timeout logic if needed for ZeroMQ
    }

    #endregion private
}