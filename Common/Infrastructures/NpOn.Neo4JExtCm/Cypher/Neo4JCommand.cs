using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.Neo4JExtCm.Cypher;

public class Neo4JCommand : NpOnDbCommand
{
    private Neo4JCommand(string? commandText)
        : base(EDb.Neo4j, commandText)
    {
    }

    public static Neo4JCommand Create(string? commandText)
    {
        return new Neo4JCommand(commandText);
    }
}
