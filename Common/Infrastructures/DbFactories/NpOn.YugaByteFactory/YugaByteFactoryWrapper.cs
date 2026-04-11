using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.DbFactories.NpOn.YugaByteFactory.FactoryResults;
using Common.Infrastructures.NpOn.YugaByteExtCm.Connections;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.DbFactories.NpOn.YugaByteFactory;

public class YugaByteFactoryWrapper : BaseDbFactoryWrapper, IYugaByteFactoryWrapper, IActionGenerator
{
    public YugaByteFactoryWrapper(
        string openConnectString, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.YugaBytePg;
        Factory = new YugaByteDriverFactory(
            option: new YugaByteConnectOption()
                .SetConnectionString(openConnectString),
            poolStore: poolStore,
            connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public YugaByteFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.YugaBytePg;
        if (connectOption is not YugaByteConnectOption)
            throw new ArgumentException("connectOption must be a YugaByteConnectOption");
        Factory = new YugaByteDriverFactory(option: connectOption, poolStore: poolStore, connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    #region Implement

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    private (string commandText, List<INpOnDbCommandParam> parameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        // Reuse Postgres utility methods for SQL generation
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

    public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            var typedParameters = npOnRepositoryCommand.Parameters?.ToList();
            return ExecuteFuncParams(npOnRepositoryCommand.CommandText, typedParameters);
        }

        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }

    #endregion Implement

    public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType,
        bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, actionType, isUseDefaultWhenNull);
        return new NpOnDbCommand(DbType, commandText, parameters);
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
