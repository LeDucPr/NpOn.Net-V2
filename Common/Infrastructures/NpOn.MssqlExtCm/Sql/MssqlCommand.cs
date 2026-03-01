using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Sql;

public class MssqlCommand : NpOnDbCommand
{
    private MssqlCommand(string? commandText)
        : base(EDb.Mssql, commandText)
    {
    }

    public static MssqlCommand Create(string? commandText)
    {
        return new MssqlCommand(commandText);
    }
}