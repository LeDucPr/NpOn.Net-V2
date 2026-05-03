using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.MySqlFactory.FactoryResults;
using Common.Infrastructures.NpOn.MySqlExtCm.Connections;
using MySqlConnector;

namespace Common.Infrastructures.DbFactories.NpOn.MySqlFactory;

public class MySqlFactoryWrapper : BaseDbFactoryWrapper, IMySqlFactoryWrapper, IActionGenerator
{
    public MySqlFactoryWrapper(
        string openConnectString, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.MySql;
        Factory = new MySqlDriverFactory(
            option: new MySqlConnectOption()
                .SetConnectionString(openConnectString),
            poolStore: poolStore,
            connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public MySqlFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.MySql;
        if (connectOption is not MySqlConnectOption)
            throw new ArgumentException("connectOption must be a MySqlConnectOption");
        Factory = new MySqlDriverFactory(option: connectOption, poolStore: poolStore, connectionNumber: connectionNumber);
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
        (string commandText, IEnumerable<MySqlParameter> npgsqlParameters) = actionType switch
        {
            ERepositoryAction.Add => domains.Cast<BaseDomain>().ToList()
                .ToMySqlParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => domains.Cast<BaseDomain>().ToList()
                .ToMySqlParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domains.Cast<BaseDomain>().ToList()
                .ToMySqlParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => domains.Cast<BaseDomain>().ToList()
                .ToMySqlParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        List<INpOnDbCommandParam> parameters = npgsqlParameters
            .Select(p => new NpOnDbCommandParam<MySqlDbType>
            {
                ParamName = p.ParameterName,
                ParamValue = p.Value ?? DBNull.Value,
                ParamType = p.MySqlDbType
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
                npOnRepositoryCommand.Parameters?.OfType<INpOnDbCommandParam /*<MySqlDbType>*/>().ToList();
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
        
        (string commandText, List<MySqlParameter> npgsqlParameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToMySqlParamsInsert(tableName),
            ERepositoryAction.Update => tableWrapper.ToMySqlParamsUpdate(tableName),
            ERepositoryAction.Merge => tableWrapper.ToMySqlParamsMerge(tableName),
            ERepositoryAction.Delete => tableWrapper.ToMySqlParamsDelete(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        var parameters = npgsqlParameters
            .Select(p => new NpOnDbCommandParam<MySqlDbType>
            {
                ParamName = p.ParameterName,
                ParamValue = p.Value ?? DBNull.Value,
                ParamType = p.MySqlDbType
            })
            .Cast<INpOnDbCommandParam>()
            .ToList();

        return new NpOnDbCommand(DbType, commandText, parameters);
    }
}