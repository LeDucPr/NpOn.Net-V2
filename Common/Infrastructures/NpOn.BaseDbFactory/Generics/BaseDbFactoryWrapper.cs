using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;

namespace Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;

public abstract class BaseDbFactoryWrapper : IDbFactoryWrapper
{
    protected IDbDriverFactory? Factory;
    protected EDb DbType;

    public EDb GetDbType() => DbType;

    /// <summary>
    /// Generic initial
    /// </summary>
    protected BaseDbFactoryWrapper()
    {
    }

    protected BaseDbFactoryWrapper(IDbDriverFactory factory, bool isUseCaching = true)
    {
        DbType = factory.GetDbType();
        Factory = factory;
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    public string? FactoryOptionCode => Factory?.DriverOptionKey;

    protected async Task<INpOnWrapperResult?> ExecuteWithConnectionAsync(Func<NpOnDbConnection, Task<INpOnWrapperResult?>> action)
    {
        if (Factory == null) return null;
        NpOnDbConnection? connection = null;
        try
        {
            connection = await Factory.GetConnectionAsync();
            if (connection == null) return null; // Không có kết nối khả dụng
            return await action(connection);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (connection != null) Factory.ReleaseConnection(connection);
        }
    }

    public async Task<INpOnWrapperResult?> ExecuteAsync(INpOnDbCommand dbCommand)
    {
        return await ExecuteWithConnectionAsync(async connection => await connection.Driver.Execute(dbCommand));
    }

    public async Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            INpOnDbCommand command = new NpOnDbCommand(DbType, queryString, parameters);
            return await connection.Driver.Execute(command);
        });
    }

    public async Task<INpOnWrapperResult?> ExecuteFuncParams<TEnumDbType>(string funcName,
        List<INpOnDbCommandParam<TEnumDbType>>? parameters) where TEnumDbType : Enum
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            var safeParams = parameters ?? [];
            INpOnDbExecFuncCommand execFuncCommand =
                new NpOnDbExecFuncCommand(DbType, funcName, parameters);
            return await connection.Driver.Execute(execFuncCommand);
        });
    }
}