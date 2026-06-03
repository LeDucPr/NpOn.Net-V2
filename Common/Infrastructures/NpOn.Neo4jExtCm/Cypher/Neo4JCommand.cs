using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.Neo4jExtCm.Cypher;

public class Neo4jCommand : NpOnDbCommand
{
    private Neo4jCommand(string? commandText)
        : base(EDb.Neo4j, commandText)
    {
    }

    public static Neo4jCommand Create(string? commandText)
    {
        return new Neo4jCommand(commandText);
    }
}
