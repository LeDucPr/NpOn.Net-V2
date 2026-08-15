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
    private readonly ConcurrentStack<NpOnDbConnection>
        _idleConnections = new(); // ConcurrentStack to keep hot connections (LIFO)

    private SemaphoreSlim? _poolSemaphore;

    // Lock used only when replacing a zombie connection with a new one (self-heal).
    // Not on the normal hot path, so it doesn't affect performance when the pool is healthy.
    private readonly object _connectionsLock = new();
    // Number of attempts to recreate a new connection when a zombie is detected, before accepting failure
    // and allowing the upper layer (BaseDbFactoryWrapper) to retry with backoff.
    private const int MaxHealAttempts = 2;

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
    /// Retrieves an available connection from the pool.
    ///
    /// FIXED "zombie connection loop" bug in the original version:
    ///   - Original: when an idle connection that was dead (IsValidSession == false) was popped and
    ///     OpenAsync() failed to reopen it, the code PUSHED THE DEAD CONNECTION BACK TO THE TOP
    ///     OF THE STACK and then threw. Since ConcurrentStack is LIFO, the next GetConnectionAsync() call almost
    ///     certainly popped the EXACT same zombie connection -> reopened -> failed -> pushed back ->
    ///     infinite loop. That connection was never replaced, and other healthy connections in the
    ///     pool suffered from "starvation" because they were always blocked by the dead connection at the top of the stack.
    ///   - Fix: when a dead idle connection could not be reopened, TryHealConnectionAsync()
    ///     is called to recreate (self-heal) a completely new connection using InitConnection() and replace it
    ///     in _connections (the total number of managed connections does not gradually decrease over time).
    ///     If recreation also fails, the zombie connection is kept in the pool (without permanent loss of capacity,
    ///     the DB might recover on a subsequent attempt) but the method DOES NOT throw
    ///     an exception — it returns null as per the existing contract (similar to a timeout), allowing the calling layer
    ///     (BaseDbFactoryWrapper) to retry with backoff.
    ///   - Always balance exactly 1 Wait() - 1 Release() on _poolSemaphore in ALL branches (including
    ///     error branches), to never leak permits -> preventing permanent pool exhaustion.
    ///
    /// This method (together with ReleaseConnection below) is what makes the EXECUTION-level retry in
    /// BaseDbFactoryWrapper.ExecuteWithConnectionAsync safe to use: when that wrapper hands back a
    /// connection that died mid-query, it is pushed here as idle, and the very next call to this
    /// method will detect it via IsValidSession, try to reopen it, and self-heal it if needed - so a
    /// retry attempt is guaranteed to get a different, healthy connection instead of the same broken
    /// object.
    /// </summary>
    public async ValueTask<NpOnDbConnection?> GetConnectionAsync()
    {
        if (_poolSemaphore == null) return null;

        long timeout = ConnectionTimeoutSessions();
        TimeSpan timeoutSpan =
            timeout > 1000
                ? TimeSpan.FromMilliseconds(timeout)
                : TimeSpan.FromSeconds(timeout); // Use milliseconds if timeout is large, otherwise seconds.

        // 1. Wait until a slot is available logically (Semaphore count) using Configured Timeout
        bool acquired = await _poolSemaphore.WaitAsync(timeoutSpan).ConfigureAwait(false);
        if (!acquired)
        {
            _logger.LogWarning(
                $"Timeout waiting for a DB connection slot after {timeoutSpan.TotalSeconds}s."); // Log warning on timeout
            return null; // Return null early on timeout, avoiding indefinite hang
        }

        // 2. Pop an available connection from the ConcurrentStack (Lock-free O(1))
        if (!_idleConnections.TryPop(out var connection))
        {
            // The queue was empty despite the semaphore granting permission -> return the slot immediately, report null for the upper layer to retry.
            _logger.LogWarning("Queue was empty despite semaphore granting access."); // Log warning if queue is empty
            _poolSemaphore.Release();
            return null;
        }

        connection.Driver.IsBusy = true; // Mark connection as busy
        connection.Driver.ResetSessionTimeout();

        // Fast path: connection is still healthy -> return immediately, no additional overhead.
        // This is the most common case and should be optimized to be as fast as possible.
        if (connection.Driver.IsValidSession)
        {
            return connection;
        }

        // Slow path: idle connection is "dead" (zombie) -> try to reopen before concluding it's completely broken.
        try
        {
            await connection.OpenAsync().ConfigureAwait(false);
            if (connection.Driver.IsValidSession)
            {
                return connection;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Idle connection is a zombie, failed to reopen, attempting to recreate a new connection.");
        }

        // Reopening failed (or still invalid after opening) -> try to recreate a new replacement connection,
        // ABSOLUTELY do not push this zombie connection back to the top of the stack and throw like the original version.
        var healed = await TryHealConnectionAsync(connection).ConfigureAwait(false);
        if (healed != null)
        {
            return healed;
        }

        // Could not save the connection & also could not create a replacement connection.
        // Keep the zombie connection in the pool (without permanent loss of capacity), return exactly 1 slot
        // semaphore that was Waited in step 1, then report null for the upper layer to retry with backoff.
        connection.Driver.IsBusy = false; // Mark connection as not busy
        _idleConnections.Push(connection);
        _poolSemaphore.Release();
        return null;
    }

    /// <summary>
    /// Attempts to recreate a completely new connection to replace a connection identified as a zombie
    /// (dead and unable to reopen), ensuring the total number of connections managed by the factory does not decrease
    /// gradually after multiple consecutive failures (unlike simply discarding a dead connection without replacement).
    /// </summary>
    private async Task<NpOnDbConnection?> TryHealConnectionAsync(NpOnDbConnection brokenConnection)
    {
        for (int attempt = 1; attempt <= MaxHealAttempts; attempt++)
        {
            try
            {
                NpOnDbConnection fresh = InitConnection();
                await fresh.OpenAsync().ConfigureAwait(false);

                if (!fresh.Driver.IsValidSession)
                    continue;

                fresh.Driver.IsBusy = true;
                fresh.Driver.ResetSessionTimeout();

                lock (_connectionsLock)
                {
                    if (_connections != null)
                    {
                        int idx = _connections.IndexOf(brokenConnection);
                        if (idx >= 0)
                            _connections[idx] = fresh;
                        else
                            _connections.Add(fresh);
                    }
                }

                _logger.LogWarning("Detected and replaced a zombie connection with a new one (self-heal).");
                return fresh;
            }
            catch (Exception ex)
            {
                // Log error if recreation fails
                _logger.LogError(ex, $"Connection recreation failed (attempt {attempt}/{MaxHealAttempts}).");
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the connection to the pool. No synchronous validation here: if the connection just used dies
    /// midway (session drop while running a query), it is still pushed to the idle stack, and will be
    /// detected + self-healed by GetConnectionAsync() (lazy validation, see above) on the next retrieval.
    /// This avoids synchronous opening/closing of connections at the time of release, which would slow down
    /// the caller waiting for results.
    /// </summary>
    public void ReleaseConnection(NpOnDbConnection? connection)
    {
        if (connection == null) return;

        connection.Driver.ResetSessionTimeout();
        connection.Driver.IsBusy = false; // Mark connection as not busy

        if (!connection.Driver.IsValidSession)
        {
            _logger.LogDebug(
                "Returning an invalid connection to the pool - it will be automatically validated/recreated on the next retrieval.");
        }

        // Return connection to the idle stack and release the semaphore slot
        _idleConnections.Push(connection);

        if (_poolSemaphore != null)
        {
            try
            {
                _poolSemaphore.Release();
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
            _idleConnections.Clear(); // Clear the stack

            // Initialize the Semaphore with the number of permits equal to the maximum number of connections
            _poolSemaphore?.Dispose();
            _poolSemaphore = new SemaphoreSlim((int)_connectionNumber, (int)_connectionNumber);

            for (int i = 0; i < _connectionNumber; i++)
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