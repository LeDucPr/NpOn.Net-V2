using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.BaseExecution;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.DbFactory.Generics;
using Npgsql;
using NpgsqlTypes;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

/// <summary>
/// Decorator class for IDbFactoryWrapper to add PostgresSQL-specific functionalities.
/// </summary>
public class NpOnPostgresFactoryWrapper(IDbFactoryWrapper dbFactoryWrapper) : INpOnPostgresFactoryWrapper
{
    #region override

    public string? FactoryOptionCode => dbFactoryWrapper.FactoryOptionCode;
    public EDb DbType => dbFactoryWrapper.DbType;

    public Task<INpOnWrapperResult?> ExecuteAsync(INpOnDbCommand dbCommand)
        => dbFactoryWrapper.ExecuteAsync(dbCommand);

    public Task<INpOnWrapperResult?> ExecuteAsync(string queryString)
        => dbFactoryWrapper.ExecuteAsync(queryString);

    public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<NpOnDbCommandParam> parameters)
        => dbFactoryWrapper.ExecuteAsync(queryString, parameters);

    public Task<INpOnWrapperResult?> ExecuteFuncParams<TEnumDbType>(string funcName,
        List<INpOnDbCommandParam<TEnumDbType>>? parameters) where TEnumDbType : Enum
        => dbFactoryWrapper.ExecuteFuncParams(funcName, parameters);

    #endregion override


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
    public Task<INpOnWrapperResult?> Execute(NpOnExecuteCommand npOnRepositoryCommand)
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