using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

public class PostgresFactoryWrapper : BaseDbFactoryWrapper, IPostgresFactoryWrapper
{
    public PostgresFactoryWrapper(
        string openConnectString, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Postgres;
        Factory = new PostgresDriverFactory(
            new PostgresConnectOption()
                .SetConnectionString(openConnectString),
            connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public PostgresFactoryWrapper(
        INpOnConnectOption connectOption, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Postgres;
        ;
        if (connectOption is not PostgresConnectOption)
            throw new ArgumentException("connectOption must be a PostgresConnectOption");
        Factory = new PostgresDriverFactory(connectOption, connectionNumber);
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
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    private (string commandText, List<NpOnDbCommandParam> npgsqlParameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, IEnumerable<NpgsqlParameter> npgsqlParameters) = actionType switch
        {
            ERepositoryAction.Add => domains.Cast<BaseDomain>().ToList().ToPostgresParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domains.Cast<BaseDomain>().ToList()
                .ToPostgresParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => domains.Cast<BaseDomain>().ToList().ToPostgresParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        List<NpOnDbCommandParam> parameters = npgsqlParameters
            .Select(p => new NpOnDbCommandParam<NpgsqlDbType>
            {
                ParamName = p.ParameterName,
                ParamValue = p.Value ?? DBNull.Value,
                ParamType = p.NpgsqlDbType
            })
            .Cast<NpOnDbCommandParam>()
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
                npOnRepositoryCommand.Parameters?.OfType<INpOnDbCommandParam<NpgsqlDbType>>().ToList();
            return ExecuteFuncParams(npOnRepositoryCommand.CommandText, typedParameters);
        }

        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }

    #endregion Implement
}