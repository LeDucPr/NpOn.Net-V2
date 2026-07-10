using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.YugaByteExtCm.Connections;

public class YugaByteConnectOption : NpOnDbConnectOption<YugaByteDriver>
{
    public IObjectPoolStore? PoolStore { get; set; }

    public override bool IsConnectValid()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Logger.LogError($"ConnectionString is required for {GetType()}");
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
            _ => false
        };
    }
}
