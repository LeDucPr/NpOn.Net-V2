using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Extensions.NpOn.BaseDbFactory.Generics;

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

    private async Task<INpOnWrapperResult?> ExecuteWithConnectionAsync(
        Func<NpOnDbConnection, Task<INpOnWrapperResult?>> action)
    {
        if (Factory == null) return null;
        NpOnDbConnection? connection = null;
        try
        {
            // retry DB calling  
            long timeout = Factory.ConnectionTimeoutSessions();
            TimeSpan timeoutSpan = timeout > 1000
                ? TimeSpan.FromMilliseconds(timeout)
                : TimeSpan.FromSeconds(timeout);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (connection == null && stopwatch.Elapsed < timeoutSpan)
            {
                connection = await Factory.GetConnectionAsync();
                if (connection != null)
                    break;
                await Task.Delay(100).ConfigureAwait(false); // Retry 100ms
            }

            if (connection == null)
                return null; // Không có kết nối khả dụng
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

    private async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithConnectionAsync(
        Func<NpOnDbConnection, Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>>> action)
    {
        if (Factory == null) return null;
        NpOnDbConnection? connection = null;
        try
        {
            // retry DB calling  
            long timeout = Factory.ConnectionTimeoutSessions();
            TimeSpan timeoutSpan = timeout > 1000
                ? TimeSpan.FromMilliseconds(timeout)
                : TimeSpan.FromSeconds(timeout);

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (connection == null && stopwatch.Elapsed < timeoutSpan)
            {
                connection = await Factory.GetConnectionAsync();
                if (connection != null)
                    break;
                await Task.Delay(100).ConfigureAwait(false); // Retry 100ms
            }

            if (connection == null)
                return null; // Không có kết nối khả dụng
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

    public async Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand)
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

    public async Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName,
        List<INpOnDbCommandParam>? parameters)
    {
        return await ExecuteWithConnectionAsync(async connection =>
        {
            // var safeParams = parameters ?? [];
            INpOnDbExecFuncCommand execFuncCommand =
                new NpOnDbExecFuncCommand(DbType, funcName, parameters);
            return await connection.Driver.Execute(execFuncCommand);
        });
    }

    public async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands)
    {
        return await ExecuteWithConnectionAsync(async connection =>
            await connection.Driver.ExecuteWithTransaction(commands));
    }
}