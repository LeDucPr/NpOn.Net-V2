using System.Data;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using Common.Infrastructures.NpOn.YugaByteExtCm.Results;
using Npgsql;

namespace Common.Infrastructures.NpOn.YugaByteExtCm.Connections;

public class YugaByteDriver : PostgresDriver
{
    public sealed override string Name { get; set; } = "NpOn-V2.YugaByteDriver";
    public sealed override string Version { get; set; } = "1.0";

    public YugaByteDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) 
        : base(option, objectPoolStore)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        var connectionString = Option.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        // Custom handling for cluster-like connection strings
        string finalConnString = connectionString;
        
        // Check if it's potentially a list of hosts without standard Npgsql prefix
        if (!connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) && 
            !connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            // Normalize separators to comma (standard for Npgsql multi-host)
            var hosts = connectionString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(h => h.Trim());
            var hostList = string.Join(",", hosts);
            finalConnString = $"Host={hostList};Database=yugabyte;User Id=yugabyte;Password=yugabyte;";
        }
        else
        {
            // Even if it has Host=, ensure we normalize delimiters for multi-host consistency if needed
            // But usually Npgsql handles comma-separated Host parameter fine.
        }

        // Add Yugabyte specific parameters for smart load balancing if not present
        if (!finalConnString.Contains("Load Balance=", StringComparison.OrdinalIgnoreCase))
        {
            finalConnString = finalConnString.TrimEnd(';') + ";Load Balance=true;";
        }

        _connection ??= new NpgsqlConnection(finalConnString);
        await _connection.OpenAsync(cancellationToken);
        
        Version = _connection.PostgreSqlVersion.ToString();
        Name = $"YugabyteDB Cluster ({_connection.Host})";
    }

    // Override fail result creators to use YugaByte specific wrappers
    protected new INpOnWrapperResult CreateFailResult(EDbError error)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(error);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }

        return new YugaByteResultSetWrapper().SetFail(error);
    }

    protected new INpOnWrapperResult CreateFailResult(Exception ex)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(ex);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }

        return new YugaByteResultSetWrapper().SetFail(ex);
    }
}
