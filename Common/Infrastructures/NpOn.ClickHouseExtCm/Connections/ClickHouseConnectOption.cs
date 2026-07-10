using Common.Extensions.NpOn.CommonDb.Connections;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Connections;

public class ClickHouseConnectOption : NpOnDbConnectOption<ClickHouseDriver>
{
    public override bool IsConnectValid()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                Logger.LogError("ConnectionString is required for {TypeName}", GetType().Name);
                throw new ArgumentNullException(nameof(ConnectionString),
                    $"{GetType().Name} requires a {nameof(ConnectionString)}.");
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
        return base.IsValid(propertyName) && !string.IsNullOrWhiteSpace(ConnectionString);
    }
}
