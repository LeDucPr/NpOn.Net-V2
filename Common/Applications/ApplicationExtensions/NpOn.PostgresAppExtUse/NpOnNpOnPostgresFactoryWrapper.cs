using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.BaseRepository;
using Common.Infrastructures.NpOn.BaseRepository.Postgres;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.DbFactory.Generics;
using NpgsqlTypes;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

/// <summary>
/// Decorator class for IDbFactoryWrapper to add PostgresSQL-specific functionalities.
/// </summary>
public class NpOnNpOnPostgresFactoryWrapper(IDbFactoryWrapper dbFactoryWrapper) : INpOnPostgresFactoryWrapper
{
    public string? FactoryOptionCode => dbFactoryWrapper.FactoryOptionCode;
    public EDb DbType => dbFactoryWrapper.DbType;

    public Task<INpOnWrapperResult?> ExecuteAsync(INpOnDbCommand dbCommand) 
        => dbFactoryWrapper.ExecuteAsync(dbCommand);

    public Task<INpOnWrapperResult?> ExecuteAsync(string queryString) 
        => dbFactoryWrapper.ExecuteAsync(queryString);

    public Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<NpOnDbCommandParam> parameters) 
        => dbFactoryWrapper.ExecuteAsync(queryString, parameters);

    public Task<INpOnWrapperResult?> ExecuteFunc(string funcName, Dictionary<string, object> parameters, bool isUseInputJson = false,
        string? isUseOutputJsonAsName = null) 
        => dbFactoryWrapper.ExecuteFunc(funcName, parameters, isUseInputJson, isUseOutputJsonAsName);

    public Task<INpOnWrapperResult?> ExecuteFuncParams<TEnumDbType>(string funcName, List<INpOnDbCommandParam<TEnumDbType>>? parameters) where TEnumDbType : Enum 
        => dbFactoryWrapper.ExecuteFuncParams(funcName, parameters);

    /// <summary>
    /// Implements the specific Execute method for PostgreSQL commands.
    /// </summary>
    public Task<INpOnWrapperResult?> Execute(NpOnRepositoryCommand npOnRepositoryCommand)
    {
        if (npOnRepositoryCommand.ExecType == EExecType.ExecFunc)
        {
            var typedParameters = npOnRepositoryCommand.Parameters?.OfType<INpOnDbCommandParam<NpgsqlDbType>>().ToList();
            return ExecuteFuncParams(npOnRepositoryCommand.CommandText, typedParameters);
        }

        return ExecuteAsync(npOnRepositoryCommand.CommandText, npOnRepositoryCommand.Parameters?.ToList() ?? []);
    }
}