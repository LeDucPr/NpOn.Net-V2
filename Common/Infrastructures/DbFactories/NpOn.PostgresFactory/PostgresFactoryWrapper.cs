using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public class PostgresFactoryWrapper : BaseDbFactoryWrapper, IPostgresFactoryWrapper, IActionGenerator
{
    public PostgresFactoryWrapper(
        string openConnectString, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Postgres;
        Factory = new PostgresDriverFactory(
            new PostgresConnectOption()
                .SetConnectionString(openConnectString),
            poolStore,
            connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public PostgresFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Postgres;
        if (connectOption is not PostgresConnectOption)
            throw new ArgumentException("connectOption must be a PostgresConnectOption");
        Factory = new PostgresDriverFactory(connectOption, poolStore, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }


    // #region override
    //
    // public Task<INpOnWrapperResult?> ExecuteAsync(INpOnDbCommand dbCommand)
    //     => ExecuteAsync(dbCommand);
    //
    // public Task<INpOnWrapperResult?> ExecuteAsync(string queryString)
    //     => ExecuteAsync(queryString);
    //
    // public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<NpOnDbCommandParam> parameters)
    //     => ExecuteAsync(queryString, parameters);
    //
    // public Task<INpOnWrapperResult?> ExecuteFuncParams<TEnumDbType>(string funcName,
    //     List<INpOnDbCommandParam<TEnumDbType>>? parameters) where TEnumDbType : Enum
    //     => ExecuteFuncParams(funcName, parameters);
    //
    // #endregion override


    #region Implement

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    private (string commandText, List<INpOnDbCommandParam> npgsqlParameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, IEnumerable<NpgsqlParameter> npgsqlParameters) = actionType switch
        {
            ERepositoryAction.Add => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        List<INpOnDbCommandParam> parameters = npgsqlParameters
            .Select(p => new NpOnDbCommandParam<NpgsqlDbType>
            {
                ParamName = p.ParameterName,
                ParamValue = p.Value ?? DBNull.Value,
                ParamType = p.NpgsqlDbType
            })
            .Cast<INpOnDbCommandParam>()
            .ToList();
        return (commandText, parameters);
    }

    /// <summary>
    /// Implements the specific Execute method for PostgreSQL commands.
    /// </summary>
    public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            var typedParameters =
                npOnRepositoryCommand.Parameters?.OfType<INpOnDbCommandParam /*<NpgsqlDbType>*/>().ToList();
            return ExecuteFuncParams(npOnRepositoryCommand.CommandText, typedParameters);
        }

        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }

    #endregion Implement

    public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType,
        bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, actionType, isUseDefaultWhenNull);
        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        return dbCommand;
    }

    public IBaseNpOnDbCommand TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        table.CheckBuildTableActionCommand(action, tableName);
        
        var tableWrapper = (INpOnTableWrapper)table;
        
        (string commandText, List<NpgsqlParameter> npgsqlParameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToPostgresParamsInsert(tableName),
            ERepositoryAction.Update => tableWrapper.ToPostgresParamsUpdate(tableName),
            ERepositoryAction.Merge => tableWrapper.ToPostgresParamsMerge(tableName),
            ERepositoryAction.Delete => tableWrapper.ToPostgresParamsDelete(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        var parameters = npgsqlParameters
            .Select(p => new NpOnDbCommandParam<NpgsqlDbType>
            {
                ParamName = p.ParameterName,
                ParamValue = p.Value ?? DBNull.Value,
                ParamType = p.NpgsqlDbType
            })
            .Cast<INpOnDbCommandParam>()
            .ToList();

        return new NpOnDbCommand(DbType, commandText, parameters);
    }
}