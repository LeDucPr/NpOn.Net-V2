using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Commands;

public class ZeroMqCommand : NpOnDbCommand
{
    public ZeroMqCommand(string commandText) : base(commandText)
    {
    }

    public ZeroMqCommand(string commandText, List<INpOnDbCommandParam>? parameters) : base(commandText, parameters)
    {
    }
}
