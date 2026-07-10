using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Connections;

public class MssqlConnectOption : NpOnDbConnectOption<MssqlDriver>
{
    public IObjectPoolStore? PoolStore { get; set; }

    public override bool IsConnectValid()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Logger.LogError($"ConnectionString is require for {GetType()}");
                throw new ArgumentNullException($"{GetType()} is require {nameof(ConnectionString)}");
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
            // nameof(SetDatabaseName) => !string.IsNullOrWhiteSpace(DatabaseName),
            _ => false
        };
    }
}