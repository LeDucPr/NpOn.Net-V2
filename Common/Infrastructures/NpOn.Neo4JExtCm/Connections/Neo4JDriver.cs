using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.Neo4JExtCm.Results;
using Neo4j.Driver;

namespace Common.Infrastructures.NpOn.Neo4JExtCm.Connections;

public class Neo4JDriver : NpOnDbDriver
{
    private IDriver? _driver;
    private readonly string _databaseName = "neo4j";

    public sealed override string Name { get; set; } = "Neo4j";
    public sealed override string Version { get; set; } = "Unknown";

    public override bool IsValidSession => _driver != null;

    public Neo4JDriver(INpOnConnectOption option) : base(option)
    {
        if (option is Neo4JConnectOption neo4JOption)
        {
            _databaseName = neo4JOption.DatabaseName;
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        try
        {
            _driver = GraphDatabase.Driver(
                Option.ConnectionString,
                AuthTokens.None
            );

            await _driver.VerifyConnectivityAsync();

            var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
            try
            {
                var result = await session.RunAsync("CALL dbms.components() YIELD name, versions RETURN name, versions");
                var record = await result.SingleAsync();
                Name = record["name"].As<string>();
                var versions = record["versions"].As<List<object>>();
                Version = versions.Count > 0 ? versions[0]?.ToString() ?? "Unknown" : "Unknown";
            }
            catch
            {
                Name = "Neo4j/openCypher";
                Version = "Unknown";
            }
            finally
            {
                await session.CloseAsync();
            }
        }
        catch (Exception)
        {
            _driver?.Dispose();
            _driver = null;
        }
    }

    public override async Task DisconnectAsync()
    {
        if (_driver != null)
        {
            await _driver.DisposeAsync();
            _driver = null;
        }
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _driver == null)
            return new Neo4JResultSetWrapper().SetFail(EDbError.Session);

        if (command is not INpOnDbCommand execCommand || string.IsNullOrWhiteSpace(execCommand.CommandText))
            return new Neo4JResultSetWrapper().SetFail(EDbError.Command);

        var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
        try
        {
            var parameters = BuildNeo4JParameters(execCommand.Parameters);
            var result = await session.RunAsync(execCommand.CommandText, parameters);
            var records = await result.ToListAsync();
            return new Neo4JResultSetWrapper(records);
        }
        catch (Exception ex)
        {
            return new Neo4JResultSetWrapper().SetFail(ex);
        }
        finally
        {
            await session.CloseAsync();
            ResetSessionTimeout();
        }
    }

    public override async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidSession || _driver == null)
            throw new InvalidOperationException("Neo4j driver is not connected.");

        var session = _driver.AsyncSession(o => o.WithDatabase(_databaseName));
        var results = new Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>();

        IBaseNpOnDbCommand[] baseNpOnDbCommands = commands as IBaseNpOnDbCommand[] ?? commands.ToArray();
        try
        {
            var tx = await session.BeginTransactionAsync();
            try
            {
                foreach (var command in baseNpOnDbCommands)
                {
                    if (command is not INpOnDbCommand execCommand || string.IsNullOrWhiteSpace(execCommand.CommandText))
                    {
                        var failResult = new Neo4JResultSetWrapper().SetFail(EDbError.Command);
                        results.Add(command, failResult);
                        break;
                    }

                    var parameters = BuildNeo4JParameters(execCommand.Parameters);
                    var cursor = await tx.RunAsync(execCommand.CommandText, parameters);
                    var records = await cursor.ToListAsync();
                    var wrapperResult = new Neo4JResultSetWrapper(records);
                    results.Add(command, wrapperResult);

                    if (!wrapperResult.Status) break;
                }

                if (results.All(r => r.Value.Status))
                    await tx.CommitAsync();
                else
                    await tx.RollbackAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            if (results.Count == 0)
            {
                var failResult = new Neo4JResultSetWrapper().SetFail(ex);
                foreach (var cmd in baseNpOnDbCommands)
                {
                    results.TryAdd(cmd, failResult);
                }
            }
        }
        finally
        {
            await session.CloseAsync();
            ResetSessionTimeout();
        }

        return results;
    }

    private static Dictionary<string, object?> BuildNeo4JParameters(List<INpOnDbCommandParam>? parameters)
    {
        var dict = new Dictionary<string, object?>();
        if (parameters == null) return dict;

        foreach (var param in parameters)
        {
            var name = param.ParamName.TrimStart('@', '$');
            dict[name] = Neo4JUtils.NormalizeToCypherValue(param.ParamValue);
        }

        return dict;
    }
}
