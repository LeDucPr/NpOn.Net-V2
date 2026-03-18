using Cassandra;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.CassandraExtCm.Results;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Connections;

public class CassandraDriver : NpOnDbDriver
{
    private ICluster? _cluster;
    private ISession? _session;
    public sealed override string Name { get; set; }
    public sealed override string Version { get; set; }

    public override bool IsValidSession => _session is { IsDisposed: false };

    public CassandraDriver(CassandraConnectOption option) : base(option)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            if (Option.IsWaitNextTransaction)
            {
                return;
            }

            await DisconnectAsync().ConfigureAwait(false);
        }

        if (_cluster == null)
        {
            var cassandraBuilder = Cluster.Builder();
            if (Option.ContactAddresses is { Length: > 0 })
            {
                cassandraBuilder.AddContactPoints(Option.ContactAddresses);
            }

            _cluster = cassandraBuilder.Build();
            Name = cassandraBuilder.ApplicationName;
            Version = cassandraBuilder.ApplicationVersion;
        }

        _session = await _cluster.ConnectAsync(Option.Keyspace).ConfigureAwait(false);
    }

    public override async Task DisconnectAsync()
    {
        if (!Option.IsShutdownImmediate)
        {
            if (_session != null)
                await _session.ShutdownAsync().ConfigureAwait(false);
            if (_cluster != null)
                await _cluster.ShutdownAsync().ConfigureAwait(false);
        }
        else
        {
            _session?.Dispose();
            _cluster?.Dispose();
        }

        _session = null;
        _cluster = null;
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _session == null)
            return new CassandraResultSetWrapper().SetFail(EDbError.Session);
        if (command is not INpOnDbCommand execCommand)
            return new CassandraResultSetWrapper().SetFail(EDbError.Command);
        if (string.IsNullOrWhiteSpace(execCommand.CommandText))
            return new CassandraResultSetWrapper().SetFail(EDbError.CommandText);

        try
        {
            var statement = new SimpleStatement(execCommand.CommandText);
            RowSet rowSet = await _session.ExecuteAsync(statement).ConfigureAwait(false);

            HashSet<string>? primaryKeys = null;
            if (rowSet.Columns.Length > 0)
            {
                // Lấy Keyspace và Table từ metadata trong RowSet (đầu tiên)
                var firstCol = rowSet.Columns[0];
                if (!string.IsNullOrEmpty(firstCol.Keyspace) && !string.IsNullOrEmpty(firstCol.Table))
                    primaryKeys = GetPrimaryKeys(firstCol.Keyspace, firstCol.Table);
            }

            return new CassandraResultSetWrapper(rowSet, primaryKeys);
        }
        catch (Exception)
        {
            return new CassandraResultSetWrapper().SetFail(EDbError.CommandTextSyntax);
        }
    }

    private HashSet<string> GetPrimaryKeys(string keyspaceName, string tableName)
    {
        var primaryKeys = new HashSet<string>();
        if (_cluster == null) return primaryKeys;

        // Metadata.GetTable là hàm đồng bộ trong driver Cassandra
        var table = _cluster.Metadata.GetTable(keyspaceName, tableName);
        if (table == null) return primaryKeys;

        if (table.PartitionKeys != null)
            foreach (var col in table.PartitionKeys)
                primaryKeys.Add(col.Name);

        if (table.ClusteringKeys != null)
            foreach (var cluster in table.ClusteringKeys)
                primaryKeys.Add(cluster.Item1.Name); // ClusteringKey - Tuple<TableColumn, SortOrder>

        return primaryKeys;
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }
}