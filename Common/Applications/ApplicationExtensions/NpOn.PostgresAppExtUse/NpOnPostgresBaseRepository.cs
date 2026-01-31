using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.BaseRepository;
using Common.Infrastructures.NpOn.BaseRepository.Postgres;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Npgsql;
using NpgsqlTypes;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public class NpOnPostgresBaseRepository(INpOnPostgresFactoryWrapper npOnPostgresFactoryWrapper) : INpOnPostgresBaseRepository
{
    private readonly EDb _dataBaseType = EDb.Postgres;

    public async Task<INpOnWrapperResult?>Add<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Add, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(_dataBaseType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await npOnPostgresFactoryWrapper.ExecuteAsync(dbCommand);
        return wrapperResult;
    }
    public async Task<INpOnWrapperResult?>Update<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Update, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(_dataBaseType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await npOnPostgresFactoryWrapper.ExecuteAsync(dbCommand);
        return wrapperResult;
    }
    public async Task<INpOnWrapperResult?>Merge<T>(IEnumerable<T> domains, bool isUseDefaultWhenNull = false) where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Merge, isUseDefaultWhenNull);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(_dataBaseType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await npOnPostgresFactoryWrapper.ExecuteAsync(dbCommand);
        return wrapperResult;
    }
    public async Task<INpOnWrapperResult?>Delete<T>(IEnumerable<T> domains) where T : BaseDomain
    {
        (string commandText, List<NpOnDbCommandParam> npgsqlParameters) = GetParams(domains, ERepositoryAction.Delete);

        INpOnDbCommand dbCommand =
            new NpOnDbCommand(_dataBaseType, commandText, npgsqlParameters);
        INpOnWrapperResult? wrapperResult = await npOnPostgresFactoryWrapper.ExecuteAsync(dbCommand);
        return wrapperResult;
    }

    public async Task<INpOnWrapperResult?> Execute(NpOnRepositoryCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            var typedParameters = npOnRepositoryCommand.Parameters?
                .OfType<INpOnDbCommandParam<NpgsqlDbType>>()
                .ToList();
            return await npOnPostgresFactoryWrapper.ExecuteFuncParams(
                npOnRepositoryCommand.CommandText, typedParameters);
        }

        if (npOnRepositoryCommand.Parameters is { Length: > 0 })
            return await npOnPostgresFactoryWrapper.ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters.ToList());
        return await npOnPostgresFactoryWrapper.ExecuteAsync(npOnRepositoryCommand.CommandText);
    }
    
    private (string commandText, List<NpOnDbCommandParam> npgsqlParameters) GetParams<T>(
        IEnumerable<T> domains, ERepositoryAction actionType, bool isUseDefaultWhenNull = false) where T : BaseDomain 
    {
        (string commandText, List<NpgsqlParameter> npgsqlParameters) = actionType switch
        {
            ERepositoryAction.Add => domains.Cast<BaseDomain>().ToList().ToPostgresParamsInsert(isUseDefaultWhenNull),
            ERepositoryAction.Update => domains.Cast<BaseDomain>().ToList().ToPostgresParamsUpdate(isUseDefaultWhenNull),
            ERepositoryAction.Delete => domains.Cast<BaseDomain>().ToList().ToPostgresParamsDelete(isUseDefaultWhenNull),
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
}