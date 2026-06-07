using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Commands;

public class ZeroMqCommand : NpOnDbCommand
{
    public Func<string, Task<string>>? Callback { get; }
    public string? Payload { get; }
    public string? MessageId { get; set; }
    public bool IsReply { get; set; }

    public ZeroMqCommand(string commandText, string? payload = null) : base(EDb.ZeroMqRunAsDbFlow, commandText)
    {
        Payload = payload;
    }

    public ZeroMqCommand(string commandText, Func<string, Task<string>> callback) : base(EDb.ZeroMqRunAsDbFlow, commandText)
    {
        Callback = callback;
    }

    public ZeroMqCommand(string commandText, List<INpOnDbCommandParam>? parameters) : base(EDb.ZeroMqRunAsDbFlow, commandText, parameters)
    {
    }
}