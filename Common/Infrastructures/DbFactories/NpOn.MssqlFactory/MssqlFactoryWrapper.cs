using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.MssqlFactory.FactoryResults;
using Common.Infrastructures.NpOn.MssqlExtCm.Connections;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.DbFactories.NpOn.MssqlFactory;

public class MssqlFactoryWrapper : BaseDbFactoryWrapper, IMssqlFactoryWrapper, IActionGenerator
{
    public MssqlFactoryWrapper(
        string openConnectString, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Mssql;
        Factory = new MssqlDriverFactory(
            option: new MssqlConnectOption()
                .SetConnectionString(openConnectString),
            poolStore: poolStore,
            connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public MssqlFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Mssql;
        if (connectOption is not MssqlConnectOption)
            throw new ArgumentException("connectOption must be a MssqlConnectOption");
        Factory = new MssqlDriverFactory(option: connectOption, poolStore: poolStore, connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    #region Implement

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<SqlParameter> parameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<SqlParameter> parameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<SqlParameter> parameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<SqlParameter> parameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
        return await ExecuteAsync(dbCommand);
    }

    private (string commandText, List<SqlParameter> parameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        List<BaseDomain> list = domains.Cast<BaseDomain>().ToList();
        return actionType switch
        {
            ERepositoryAction.Add => list.ToMssqlParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => list.ToMssqlParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => list.ToMssqlParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => list.ToMssqlParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };
    }

    public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnRepositoryCommand)
    {
        // ExecType.ExecFunc for MSSQL would normally be handled via Stored Procs or similar
        // For now, we follow the pattern of the base execute
        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }

    #endregion Implement

    public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType,
        bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<SqlParameter> parameters) =
            GetParams(domains, actionType, isUseDefaultWhenNull);
        return new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
    }

    public IBaseNpOnDbCommand TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        if (table is not INpOnTableWrapper tableWrapper)
            throw new ArgumentException("table must be an INpOnTableWrapper");

        (string commandText, List<SqlParameter> parameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToMssqlParamsInsert(tableName),
            ERepositoryAction.Update => tableWrapper.ToMssqlParamsUpdate(tableName),
            ERepositoryAction.Delete => tableWrapper.ToMssqlParamsDelete(tableName),
            ERepositoryAction.Merge => tableWrapper.ToMssqlParamsMerge(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        return new NpOnDbCommand(DbType, commandText, parameters.Cast<INpOnDbCommandParam>().ToList());
    }
}
