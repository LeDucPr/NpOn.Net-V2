using Common.Extensions.NpOn.BaseDbFactory.Generics;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.CassandraFactory.FactoryResults;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.CassandraFactory;

public class CassandraFactoryWrapper : BaseDbFactoryWrapper, ICassandraFactoryWrapper, IActionGenerator
{
    public CassandraFactoryWrapper(
        string keyspace, string[] contactAddresses, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Cassandra;
        Factory = new CassandraDriverFactory(
#pragma warning disable CS0618 // Type or member is obsolete
            new CassandraConnectOption()
                .SetKeyspace(keyspace)
#pragma warning restore CS0618 // Type or member is obsolete
                .SetContactAddresses(contactAddresses),
            poolStore,
            connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public CassandraFactoryWrapper(
        INpOnConnectOption connectOption, IObjectPoolStore? poolStore = null, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = EDb.Cassandra;
        if (connectOption is not CassandraConnectOption)
            throw new ArgumentException("connectOption must be a CassandraConnectOption");
        Factory = new CassandraDriverFactory(connectOption, poolStore, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    #region Implement

    public async Task<INpOnWrapperResult?> Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, parameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, parameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false)
        where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) =
            GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, parameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, parameters);
        INpOnWrapperResult? wrapperResult = await ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    private (string commandText, List<INpOnDbCommandParam> parameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<INpOnDbCommandParam> parameters) = actionType switch
        {
            ERepositoryAction.Add => domains.Cast<BaseDomain>().ToList()
                .ToCassandraParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => domains.Cast<BaseDomain>().ToList()
                .ToCassandraParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domains.Cast<BaseDomain>().ToList()
                .ToCassandraParamsDelete(isUseDefaultWhenNull),
            ERepositoryAction.Merge => domains.Cast<BaseDomain>().ToList()
                .ToCassandraParamsMerge(isUseDefaultWhenNull),
            _ => throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null)
        };

        return (commandText, parameters);
    }

    public Task<INpOnWrapperResult?> Execute(NpOnDbExecuteCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            var typedParameters =
                npOnRepositoryCommand.Parameters?.OfType<INpOnDbCommandParam>().ToList();
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
        INpOnDbCommand dbCommand =
            new NpOnDbCommand(DbType, commandText, parameters);
        return dbCommand;
    }

    public IBaseNpOnDbCommand TableActionCommand(INpOnWrapperResult table, ERepositoryAction action, string tableName)
    {
        table.CheckBuildTableActionCommand(action, tableName);
        
        var tableWrapper = (INpOnTableWrapper)table;
        
        (string commandText, List<INpOnDbCommandParam> parameters) = action switch
        {
            ERepositoryAction.Add => tableWrapper.ToCassandraParamsInsert(tableName),
            ERepositoryAction.Update => tableWrapper.ToCassandraParamsUpdate(tableName),
            ERepositoryAction.Merge => tableWrapper.ToCassandraParamsMerge(tableName),
            ERepositoryAction.Delete => tableWrapper.ToCassandraParamsDelete(tableName),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        return new NpOnDbCommand(DbType, commandText, parameters);
    }
}
