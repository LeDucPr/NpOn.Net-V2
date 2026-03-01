using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Cql;

public class CassandraCommand : NpOnDbCommand
{
    private CassandraCommand(string? commandText)
        : base(EDb.Cassandra, commandText)
    {
    }

    public static CassandraCommand Create(string? commandText)
    {
        return new CassandraCommand(commandText);
    }
}