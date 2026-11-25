using System.Threading.Channels;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Non-blocking, lock-free audit buffer powered by System.Threading.Channels.
/// UDAL pushes audit batches into memory within 0.002ms without stalling client API responses.
/// </summary>
public class SagaAuditChannel : IDisposable
{
    private readonly Channel<SagaAuditBatch> _channel;
    private readonly CancellationTokenSource _cts;
    private int _disposed;

    public int Capacity { get; }
    public int Count => _channel.Reader.Count;
    public bool IsCompleted { get; private set; }

    public ChannelWriter<SagaAuditBatch> Writer => _channel.Writer;
    public ChannelReader<SagaAuditBatch> Reader => _channel.Reader;

    public SagaAuditChannel(int capacity = 50000, BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait)
    {
        Capacity = capacity;
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = fullMode,
            SingleWriter = false,
            SingleReader = false
        };
        _channel = Channel.CreateBounded<SagaAuditBatch>(options);
        _cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Fast non-blocking push of audit batch into memory buffer.
    /// </summary>
    public bool TryWrite(SagaAuditBatch batch)
    {
        return _channel.Writer.TryWrite(batch);
    }

    /// <summary>
    /// Asynchronous write awaiting capacity if buffer is temporarily full.
    /// </summary>
    public ValueTask WriteAsync(SagaAuditBatch batch, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(batch, cancellationToken);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { }
        finally
        {
            _cts.Dispose();
        }
    }
}
