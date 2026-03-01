using Common.Extensions.NpOn.CommonDb.Connections;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructures.NpOn.ElasticSearchExtCm.Connections;

/// <summary>
/// Defines connection options for ElasticSearch, inheriting from the common DbNpOnConnectOption.
/// </summary>
public class ElasticSearchConnectOption : DbNpOnConnectOption<ElasticSearchDriver>
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