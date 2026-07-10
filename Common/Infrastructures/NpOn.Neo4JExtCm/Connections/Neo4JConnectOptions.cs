using Common.Extensions.NpOn.CommonDb.Connections;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.Neo4JExtCm.Connections;

public class Neo4JConnectOption : NpOnDbConnectOption<Neo4JDriver>
{
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
            nameof(SetDatabaseName) => !string.IsNullOrWhiteSpace(DatabaseName),
            _ => false
        };
    }
}