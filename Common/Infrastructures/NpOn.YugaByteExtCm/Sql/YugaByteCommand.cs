using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.YugaByteExtCm.Sql;

public class YugaByteCommand : NpOnDbCommand
{
    private YugaByteCommand(string? commandText)
        : base(EDb.YugaBytePg, commandText)
    {
    }

    public static YugaByteCommand Create(string? commandText)
    {
        return new YugaByteCommand(commandText);
    }
}
