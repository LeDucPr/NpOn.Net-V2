using Cassandra;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Infrastructures.NpOn.CassandraExtCm.Results;

namespace Common.Infrastructures.NpOn.CassandraExtCm.Connections;

public class CassandraDriver : NpOnDbDriver
{
    private ICluster? _cluster;
    private ISession? _session;
    private readonly IObjectPool<CassandraResultSetWrapper>? _resultSetPool;
    public sealed override string Name { get; set; }
    public sealed override string Version { get; set; }

    public override bool IsValidSession => _session is { IsDisposed: false };

    public CassandraDriver(CassandraConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            _resultSetPool = objectPoolStore.GetPool(() => new CassandraResultSetWrapper());
        }
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
            return CreateFailResult(EDbError.Session);
        if (command is not INpOnDbCommand execCommand)
            return CreateFailResult(EDbError.Command);
        if (string.IsNullOrWhiteSpace(execCommand.CommandText))
            return CreateFailResult(EDbError.CommandText);

        try
        {
            SimpleStatement statement;
            if (execCommand.Parameters != null && execCommand.Parameters.Any())
            {
                var values = new List<object?>();
                foreach (var prm in execCommand.Parameters)
                {
                    var targetDbType = ECassandraDbType.Unknown;
                    if (prm is NpOnDbCommandParam<ECassandraDbType> typedParam)
                        targetDbType = typedParam.ParamType;

                    // value and type logic
                    if (targetDbType != ECassandraDbType.Unknown)
                    {
                        // Here you can do custom casting inside driver if specific ECassandraDbType requires it
                        // For Cassandra driver DataStax, most primitive matching is fine
                        values.Add(prm.ParamValue);
                    }
                    else
                    {
                        values.Add(prm.ParamValue);
                    }
                }
                statement = new SimpleStatement(execCommand.CommandText, values.ToArray());
            }
            else
            {
                statement = new SimpleStatement(execCommand.CommandText);
            }
            
            RowSet rowSet = await _session.ExecuteAsync(statement).ConfigureAwait(false);

            HashSet<string>? primaryKeys = null;
            if (rowSet.Columns.Length > 0)
            {
                // Lấy Keyspace và Table từ metadata trong RowSet (đầu tiên)
                var firstCol = rowSet.Columns[0];
                if (!string.IsNullOrEmpty(firstCol.Keyspace) && !string.IsNullOrEmpty(firstCol.Table))
                    primaryKeys = GetPrimaryKeys(firstCol.Keyspace, firstCol.Table);
            }

            if (_resultSetPool != null)
            {
                var wrapper = _resultSetPool.Get();
                wrapper.Reset();
                wrapper.Init(rowSet, primaryKeys);
                wrapper.ReturnToPool = w => _resultSetPool.Return(w);
                return wrapper;
            }

            return new CassandraResultSetWrapper(rowSet, primaryKeys);
        }
        catch (Exception ex)
        {
            return CreateFailResult(ex);
        }
    }

    private INpOnWrapperResult CreateFailResult(EDbError error)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(error);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }
        return new CassandraResultSetWrapper().SetFail(error);
    }

    private INpOnWrapperResult CreateFailResult(Exception ex)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(ex);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }
        return new CassandraResultSetWrapper().SetFail(ex);
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