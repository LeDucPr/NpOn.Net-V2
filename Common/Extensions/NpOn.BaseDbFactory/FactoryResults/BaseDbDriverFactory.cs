using System.Collections.Concurrent;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Common.Extensions.NpOn.BaseDbFactory.FactoryResults;

public interface IDbDriverFactory
{
    #region properties

    /// <summary>
    /// Available connection
    /// </summary>
    public int GetAliveConnectionNumbers { get; }

    public long ConnectionTimeoutSessions();

    public int GetConnectionNumbers { get; }
    public EDb GetDbType();

    /// <summary> List of valid connections. </summary>
    public List<NpOnDbConnection>? ValidConnections { get; }

    public NpOnDbConnection? FirstValidConnection { get; }
    public string DriverOptionKey { get; }

    /// <summary> Retrieves an available connection from the pool. </summary>
    ValueTask<NpOnDbConnection?> GetConnectionAsync();

    /// <summary> Returns the connection to the pool. </summary>
    void ReleaseConnection(NpOnDbConnection connection);

    #endregion properties


    #region Create Connections

    IDbDriverFactory WithDatabaseType(EDb eDb);
    IDbDriverFactory WithOption(INpOnConnectOption option);
    IDbDriverFactory CreateConnections(int connectionNumber = 1);
    Task<IDbDriverFactory> Reset(bool isResetParameters = false);

    Task<int> OpenConnections(int connectionNumber = 1, bool isAutoFixConnectionNumber = true,
        bool isUseException = false);

    #endregion Create Connections
}

public abstract class BaseDbDriverFactory : IDbDriverFactory
{
    #region private parameters

    protected EDb? DbType;
    protected INpOnConnectOption? Option;
    private int? _connectionNumber;

    // ConcurrentStack to keep hot connections (LIFO)
    private readonly ConcurrentStack<NpOnDbConnection> _idleConnections = new();

    #endregion private parameters


    #region implement properties

    private readonly ILogger<BaseDbDriverFactory> _logger = new Logger<BaseDbDriverFactory>(new NullLoggerFactory());
    private List<NpOnDbConnection>? _connections;
    public int GetAliveConnectionNumbers => _connections?.Count(c => c.Driver.IsValidSession) ?? 0;
    public int GetConnectionNumbers => _connections?.Count ?? 0;
    public EDb GetDbType() => DbType ?? EDb.Unknown;

    public List<NpOnDbConnection>? ValidConnections =>
        _connections?.Where(c => c.Driver.IsValidSession).ToList();

    private List<NpOnDbConnection>? InvalidConnections =>
        _connections?.Where(c => !c.Driver.IsValidSession).ToList();

    public NpOnDbConnection? FirstValidConnection => _connections?.FirstOrDefault(c => c.Driver.IsValidSession);
    public string DriverOptionKey => Option?.Code ?? throw new Exception(EDbError.Connection.GetDisplayName());

    #endregion implement properties


    #region Create Connections

    public long ConnectionTimeoutSessions() => Option?.ConnectionTimeoutSessions ?? 30;

    public BaseDbDriverFactory(EDb dbType, INpOnConnectOption option, int connectionNumber = 1)
    {
        if (!option.IsConnectValid())
            throw new ArgumentException("Config Option for Database is Invalid.", nameof(option));
        DbType = dbType;
        Option = option;
        _connectionNumber = connectionNumber;
        SelfCreateConnections(EConnectLink.SelfValidateConnection.GetDisplayName());
    }

    public IDbDriverFactory WithDatabaseType(EDb eDb)
    {
        DbType = eDb;
        SelfCreateConnections(EConnectLink.SelfValidateConnection.GetDisplayName());
        return this;
    }

    public IDbDriverFactory WithOption(INpOnConnectOption option)
    {
        Option = option;
        SelfCreateConnections(EConnectLink.SelfValidateConnection.GetDisplayName());
        return this;
    }

    public IDbDriverFactory CreateConnections(int connectionNumber = 1)
    {
        _connectionNumber = connectionNumber;
        SelfCreateConnections(EConnectLink.SelfValidateConnection.GetDisplayName());
        return this;
    }

    public async Task<IDbDriverFactory> Reset(bool isResetParameters = false)
    {
        if (isResetParameters)
        {
            DbType = null;
            Option = null;
            _connectionNumber = null;
        }

        if (_connections == null) return this;
        foreach (var connection in _connections) await connection.CloseAsync();
        return this;
    }

    public async Task<int> OpenConnections(int connectionNumber = 1, bool isAutoFixConnectionNumber = true,
        bool isUseException = false)
    {
        try
        {
            if (_connections == null)
                throw new Exception("connection not initialized");

            if (connectionNumber <= _connectionNumber || isAutoFixConnectionNumber)
                connectionNumber = (int)_connectionNumber!;
            else
                throw new Exception("The number of connections attempted to be initiated has exceeded the limit");

            List<NpOnDbConnection>? invalidConnections = InvalidConnections;
            if (invalidConnections is not { Count : > 0 })
            {
                throw new Exception(
                    $"no longer available connection. Full connection ({connectionNumber}/{_connectionNumber})");
            }

            if (GetAliveConnectionNumbers == 0 && invalidConnections.Count > 0) // open 1 (performance with many)
                await invalidConnections.First().OpenAsync();

            // foreach (var invalidConnection in invalidConnections)
            //     await invalidConnection.OpenAsync();

            if (ValidConnections is not { Count : > 0 } && isUseException)
            {
                throw new Exception("Cannot open any Connections");
            }

            return ValidConnections?.Count ?? 0;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception.Message);
            if (isUseException)
                throw new Exception("Cannot open any Connections");
            return 0;
        }
    }

    protected abstract NpOnDbConnection InitConnection();

    /// <summary>
    /// Retrieves an available connection.
    /// Fast Path: LIFO pop from idle stack (lock-free O(1)).
    /// Elastic Native Pooling: If idle stack is empty, allocates fresh connection delegating to the driver's native connection pool.
    /// Zero semaphore contention, eliminating high concurrency bottlenecks.
    /// </summary>
    public async ValueTask<NpOnDbConnection?> GetConnectionAsync()
    {
        while (_idleConnections.TryPop(out var connection))
        {
            connection.Driver.IsBusy = true;
            connection.Driver.ResetSessionTimeout();

            if (connection.Driver.IsValidSession)
            {
                return connection;
            }

            try
            {
                await connection.OpenAsync().ConfigureAwait(false);
                if (connection.Driver.IsValidSession)
                {
                    return connection;
                }
            }
            catch
            {
                // Discard dead connection and try next or instantiate fresh
            }
        }

        // Elastic scaling via driver's native pool
        try
        {
            var fresh = InitConnection();
            await fresh.OpenAsync().ConfigureAwait(false);
            fresh.Driver.IsBusy = true;
            fresh.Driver.ResetSessionTimeout();
            return fresh;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create and open DB connection via driver native pool.");
            return null;
        }
    }

    /// <summary>
    /// Returns the connection to the pool.
    /// Recycles healthy connection into LIFO stack if under capacity, else closes and returns socket to driver's native pool.
    /// </summary>
    public void ReleaseConnection(NpOnDbConnection? connection)
    {
        if (connection == null) return;

        connection.Driver.ResetSessionTimeout();
        connection.Driver.IsBusy = false;

        int maxIdle = Math.Max(50, _connectionNumber ?? 10);
        if (connection.Driver.IsValidSession && _idleConnections.Count < maxIdle)
        {
            _idleConnections.Push(connection);
        }
        else
        {
            _ = connection.CloseAsync();
        }
    }


    private IDbDriverFactory SelfCreateConnections(string? eValidateString)
    {
        try
        {
            if (DbType == null)
            {
                throw new InvalidOperationException(
                    "Database type has not been set. Call WithDatabaseType() before creating connections.");
            }

            if (Option == null)
            {
                throw new InvalidOperationException(
                    "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
            }

            bool validateOption = eValidateString == null
                ? !Option.IsValid()
                : !Option.IsValidRequireFromBase(eValidateString);
            if (validateOption)
            {
                throw new InvalidOperationException(
                    "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
            }

            if (_connectionNumber == null)
            {
                throw new InvalidOperationException(
                    "Connection number have not been set or are invalid. Call CreateConnections() before creating connections.");
            }

            if (typeof(INpOnConnectOption) == Option.GetType())
            {
                throw new TypeInitializationException(typeof(INpOnConnectOption).ToString(),
                    new Exception("Need to configure driver correctly"));
            }

            _connections = new List<NpOnDbConnection>();
            _idleConnections.Clear(); // Clear the stack

            int preOpenCount = Math.Min(_connectionNumber ?? 1, 10);
            for (int i = 0; i < preOpenCount; i++)
            {
                NpOnDbConnection? connection = InitConnection();
                if (connection == null)
                    throw new NotSupportedException($"The database type '{DbType}' is not supported.");

                _connections.Add(connection);
                _idleConnections.Push(connection);
            }
        }
        catch (ArgumentException exception)
        {
            _logger.LogError(exception.Message);
        }
        catch (NotImplementedException exception)
        {
            _logger.LogError(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogError(exception.Message);
        }

        return this;
    }

    #endregion Create Connections
}