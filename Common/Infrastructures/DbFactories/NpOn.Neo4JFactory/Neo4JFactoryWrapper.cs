using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.Neo4JExtCm.Connections;
using Common.Infrastructures.NpOn.Neo4JExtCm.Results;

namespace Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory;

public class Neo4JFactoryWrapper : BaseDbFactoryWrapper, INeo4JFactoryWrapper, IActionGenerator
{
    public Neo4JFactoryWrapper(
        string openConnectString, string databaseName, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Neo4j;
        INpOnConnectOption? option = new Neo4JConnectOption()
            .SetConnectionString(openConnectString)
            .SetDatabaseName(databaseName);
        if (option == null)
            throw new ArgumentNullException(nameof(option));

        Factory = new Neo4JDriverFactory(option, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public Neo4JFactoryWrapper(
        INpOnConnectOption connectOption, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Neo4j;
        if (connectOption is not Neo4JConnectOption)
            throw new ArgumentException("connectOption must be a Neo4jConnectOption");
        Factory = new Neo4JDriverFactory(option: connectOption, connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> neo4JParameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, neo4JParameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> neo4JParameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, neo4JParameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> neo4JParameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, neo4JParameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> neo4JParameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, neo4JParameters);
        return await ExecuteAsync(dbCommand);
    }

    private (string commandText, List<INpOnDbCommandParam> cypherParameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        var domainList = domains.Cast<BaseDomain>().ToList();
        var (commandText, parameters) = actionType switch
        {
            ERepositoryAction.Add => domainList.ToNeo4JParamsCreate(isUseDefaultWhenNull),
            ERepositoryAction.Update => domainList.ToNeo4JParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domainList.ToNeo4JParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => domainList.ToNeo4JParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        // FIX: Using NpOnDbCommandParam<ENeo4jDbType> enum parameter instead of <object>
        var dbParameters = parameters.Select(kvp => new NpOnDbCommandParam<ENeo4jDbType>
        {
            ParamName = kvp.Key,
            ParamValue = kvp.Value ?? DBNull.Value,
            ParamType = kvp.Value != null ? Neo4JUtils.GetENeo4JDbType(kvp.Value.GetType()) : ENeo4jDbType.Unknown
        }).Cast<INpOnDbCommandParam>().ToList();

        return (commandText, dbParameters);
    }

    public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            return ExecuteFuncParams(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList());
        }

        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }

    public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType,
        bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> cypherParameters) =
            GetParams(domains, actionType, isUseDefaultWhenNull);
        return new NpOnDbCommand(DbType, commandText, cypherParameters);
    }

    public IBaseNpOnDbCommand TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        table.CheckBuildTableActionCommand(action, tableName);
        var tableWrapper = (INpOnTableWrapper)table;
        
        var (commandText, parameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToNeo4JParamsCreate(tableName),
            ERepositoryAction.Update => tableWrapper.ToNeo4JParamsUpdate(tableName),
            ERepositoryAction.Merge => tableWrapper.ToNeo4JParamsMerge(tableName),
            ERepositoryAction.Delete => tableWrapper.ToNeo4JParamsDelete(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        // FIX: Using NpOnDbCommandParam<ENeo4jDbType> enum parameter instead of <object>
        var dbParameters = parameters.Select(kvp => new NpOnDbCommandParam<ENeo4jDbType>
        {
            ParamName = kvp.Key,
            ParamValue = kvp.Value ?? DBNull.Value,
            ParamType = kvp.Value != null ? Neo4JUtils.GetENeo4JDbType(kvp.Value.GetType()) : ENeo4jDbType.Unknown
        }).Cast<INpOnDbCommandParam>().ToList();

        return new NpOnDbCommand(DbType, commandText, dbParameters);
    }
}
