using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Sql;

public class MySqlCommand : NpOnDbCommand
{
    private MySqlCommand(string? commandText)
        : base(EDb.MySql, commandText)
    {
    }

    public static MySqlCommand Create(string? commandText)
    {
        return new MySqlCommand(commandText);
    }
}