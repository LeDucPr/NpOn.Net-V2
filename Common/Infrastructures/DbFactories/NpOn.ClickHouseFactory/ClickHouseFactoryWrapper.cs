using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory.FactoryResults;
using Common.Infrastructures.NpOn.ClickHouseExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;

public class ClickHouseFactoryWrapper : BaseDbFactoryWrapper, IClickHouseFactoryWrapper, IActionGenerator
{
    public ClickHouseFactoryWrapper(
        string openConnectString, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.ClickHouse;
        var op = new ClickHouseConnectOption();
        op.SetConnectionString(openConnectString);
        Factory = new ClickHouseDriverFactory(
            option: op,
            poolStore: poolStore,
            connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public ClickHouseFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.ClickHouse;
        if (connectOption is not ClickHouseConnectOption)
            throw new ArgumentException("connectOption must be a ClickHouseConnectOption");
        Factory = new ClickHouseDriverFactory(option: connectOption, poolStore: poolStore, connectionNumber: connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    #region Implement

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        var (commandText, parameters) = GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);
        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        var (commandText, parameters) = GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);
        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        var (commandText, parameters) = GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);
        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        var (commandText, parameters) = GetParams<T>(domains, ERepositoryAction.Delete);
        INpOnDbCommand dbCommand = new NpOnDbCommand(DbType, commandText, parameters);
        return await ExecuteAsync(dbCommand);
    }

    private (string commandText, List<INpOnDbCommandParam> parameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        List<BaseDomain> list = domains.Cast<BaseDomain>().ToList();
        return actionType switch
        {
            ERepositoryAction.Add => list.ToClickHouseParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => list.ToClickHouseParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => list.ToClickHouseParamsDelete(),
            ERepositoryAction.Merge => list.ToClickHouseParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };
    }

    #endregion Implement

    public IBaseNpOnDbCommand CommandBuilder<T>(IEnumerable<T> domains, ERepositoryAction actionType,
        bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        var (commandText, parameters) = GetParams(domains, actionType, isUseDefaultWhenNull);
        return new NpOnDbCommand(DbType, commandText, parameters);
    }

    public IBaseNpOnDbCommand TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        if (table is not INpOnTableWrapper tableWrapper)
            throw new ArgumentException("table must be an INpOnTableWrapper");

        var (commandText, parameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToClickHouseParamsInsert(tableName),
            ERepositoryAction.Update => tableWrapper.ToClickHouseParamsUpdate(tableName),
            ERepositoryAction.Delete => tableWrapper.ToClickHouseParamsDelete(tableName),
            ERepositoryAction.Merge => tableWrapper.ToClickHouseParamsMerge(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        return new NpOnDbCommand(DbType, commandText, parameters);
    }
}
