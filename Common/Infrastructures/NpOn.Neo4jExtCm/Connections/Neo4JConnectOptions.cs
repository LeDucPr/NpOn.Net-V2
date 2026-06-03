using Common.Extensions.NpOn.CommonDb.Connections;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.Neo4jExtCm.Connections;

public class Neo4jConnectOption : DbNpOnConnectOption<Neo4jDriver>
{
    public string DatabaseName { get; private set; } = "neo4j";

    public Neo4jConnectOption SetNeo4jDatabase(string databaseName)
    {
        DatabaseName = databaseName;
        return this;
    }

    public override bool IsConnectValid()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Logger.LogError($"ConnectionString is required for {GetType()} (e.g. bolt://localhost:7687)");
                throw new ArgumentNullException($"{GetType()} requires {nameof(ConnectionString)}");
            }
        }
        catch (ArgumentNullException)
        {
            return false;
        }

        return base.IsValid();
    }

    public override bool IsValid(string? propertyName = null)
    {
        if (!base.IsValid())
            return false;
        if (propertyName == null)
            return true;
        return propertyName switch
        {
            nameof(SetConnectionString) => !string.IsNullOrWhiteSpace(ConnectionString),
            nameof(SetNeo4jDatabase)    => !string.IsNullOrWhiteSpace(DatabaseName),
            _                           => false
        };
    }
}
