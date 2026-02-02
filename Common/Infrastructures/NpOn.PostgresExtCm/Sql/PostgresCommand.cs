using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Sql;

public class PostgresCommand : NpOnDbCommand
{
    private PostgresCommand(string? commandText)
        : base(EDb.Postgres, commandText)
    {
    }

    public static PostgresCommand Create(string? commandText)
    {
        return new PostgresCommand(commandText);
    }
}