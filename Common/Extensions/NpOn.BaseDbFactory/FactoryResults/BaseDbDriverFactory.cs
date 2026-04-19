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
    /// Connection khả dụng
    /// </summary>
    public int GetAliveConnectionNumbers { get; }

    public int GetConnectionNumbers { get; }

    public EDb GetDbType();

    public List<NpOnDbConnection>? ValidConnections { get; }
    public NpOnDbConnection? FirstValidConnection { get; }
    public string DriverOptionKey { get; }

    /// <summary> Lấy một kết nối khả dụng từ pool. </summary>
    Task<NpOnDbConnection?> GetConnectionAsync();

    /// <summary> Trả kết nối về lại pool. </summary>
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

    // ConcurrentDictionary để theo dõi trạng thái kết nối (true = using, false = relaxing)
    private readonly ConcurrentQueue<NpOnDbConnection> _idleConnections = new();
    private SemaphoreSlim? _poolSemaphore;

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

    public async Task<NpOnDbConnection?> GetConnectionAsync()
    {
        if (_poolSemaphore == null) return null;

        // 1. Wait until a slot is available logically (Semaphore count)
        await _poolSemaphore.WaitAsync();

        try
        {
            // 2. Dequeue an available connection from the ConcurrentQueue (Lock-free O(1))
            if (_idleConnections.TryDequeue(out var connection))
            {
                // Verify and open if the session is no longer valid
                if (!connection.Driver.IsValidSession)
                {
                    // OpenAsync is handled asynchronously outside of any global lock
                    await connection.OpenAsync();
                }
                return connection;
            }

            // If we reach here, it means the queue was empty despite the semaphore granting access.
            _logger.LogWarning("Queue was empty despite semaphore granting access.");
            _poolSemaphore.Release();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire or open a connection.");
            _poolSemaphore.Release(); // Return slot to pool on failure
            throw;
        }
    }

    public void ReleaseConnection(NpOnDbConnection? connection)
    {
        if (connection == null) return;

        // Return connection to the idle queue and release the semaphore slot
        _idleConnections.Enqueue(connection);
        
        if (_poolSemaphore != null)
        {
            try
            {
                _poolSemaphore.Release();
                // Logging high-frequency events should be avoided in performance critical paths
                // _logger.LogInformation("({dbType}) Remaining available connections: {Count}", DbType, _poolSemaphore.CurrentCount);
            }
            catch (SemaphoreFullException)
            {
                // Safe guard against incorrect concurrent releases
            }
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
            while (_idleConnections.TryDequeue(out _)) { } // Clear the queue

            // Initialize the Semaphore with the number of permits equal to the maximum number of connections
            _poolSemaphore?.Dispose();
            _poolSemaphore = new SemaphoreSlim((int)_connectionNumber, (int)_connectionNumber);

            for (int i = 0; i < _connectionNumber; i++)
            {
                NpOnDbConnection? connection = InitConnection();
                if (connection == null)
                    throw new NotSupportedException($"The database type '{DbType}' is not supported.");
                
                _connections.Add(connection);
                _idleConnections.Enqueue(connection);
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