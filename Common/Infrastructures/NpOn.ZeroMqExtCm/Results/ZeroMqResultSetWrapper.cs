using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using NetMQ;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Results;

public class ZeroMqResultSetWrapper : NpOnWrapperResult
{
    private NetMQMessage? _message;
    private int _frameIndex;

    public ZeroMqResultSetWrapper()
    {
        // Default constructor for pooling
    }

    public ZeroMqResultSetWrapper Init(NetMQMessage message)
    {
        _message = message;
        _frameIndex = 0;
        Status = true;
        return this;
    }

    public ZeroMqResultSetWrapper Init(string messageString)
    {
        _message = new NetMQMessage();
        _message.Append(messageString);
        _frameIndex = 0;
        Status = true;
        return this;
    }

    public override bool Read()
    {
        if (_message == null || _frameIndex >= _message.FrameCount)
        {
            return false;
        }
        // For ZeroMQ, each frame could be considered a "row" or part of a message.
        // This implementation assumes a simple message where each frame is a distinct piece of data.
        // More complex scenarios would require a defined message structure.
        return true;
    }

    public override INpOnCell GetCell(string name)
    {
        // This method needs to be adapted based on how ZeroMQ messages are structured.
        // For now, it's a placeholder.
        return new ZeroMqCell { Value = "N/A" };
    }

    public override INpOnCell GetCell(int ordinal)
    {
        if (_message == null || ordinal >= _message.FrameCount)
        {
            return new ZeroMqCell { Value = null };
        }
        return new ZeroMqCell { Value = _message[_frameIndex].ConvertToString() };
    }

    public override void Reset()
    {
        base.Reset();
        _message = null;
        _frameIndex = 0;
    }

    public override void Dispose()
    {
        _message?.Dispose();
        _message = null;
        base.Dispose();
    }

    public override int FieldCount => _message?.FrameCount ?? 0;
}
