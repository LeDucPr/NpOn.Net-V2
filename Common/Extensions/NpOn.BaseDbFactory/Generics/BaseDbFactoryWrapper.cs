using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Microsoft.Extensions.Logging;

namespace Common.Extensions.NpOn.BaseDbFactory.Generics;

public abstract class BaseDbFactoryWrapper : IDbFactoryWrapper
{
    protected IDbDriverFactory? Factory;
    protected EDb DbType;
    private readonly ILogger? _logger;

    // Retry configuration when acquiring a connection from the pool (i.e. we don't have one yet).
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMilliseconds(4000);
    private const double RetryBackoffMultiplier = 1.5;

    // Retry configuration for a connection that dies WHILE a command is executing. This is a
    // DIFFERENT concern from the acquisition retry above - see the XML doc on
    // ExecuteWithConnectionAsync for the full distinction between the two failure domains.
    // 1 initial attempt + 1 retry. Kept small on purpose: retrying a write is "at-least-once", not
    // "exactly-once" (see the note in the XML doc below), so this should not be bumped up casually.
    private const int MaxExecutionRetryAttempts = 3;

    public EDb GetDbType() => DbType;

    protected BaseDbFactoryWrapper(ILogger? logger = null)
    {
        _logger = logger;
    }

    protected BaseDbFactoryWrapper(IDbDriverFactory factory, bool isUseCaching = true, ILogger? logger = null)
    {
        DbType = factory.GetDbType();
        Factory = factory;
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
        _logger = logger;
    }

    public string? FactoryOptionCode => Factory?.DriverOptionKey;

    /// <summary>
    /// Generic function: acquires a connection from the pool, executes an action, and ALWAYS returns the connection to the pool.
    ///
    /// Fixed critical bugs from the original version:
    ///  1) "return connection;" in the middle of the acquire loop returned the wrong type (NpOnDbConnection instead of
    ///     TResult) -> would not compile. If forced to run, action() (the actual SQL command)
    ///     WOULD NEVER be called, because the code returned early before reaching the action() call.
    ///  2) Factory.ReleaseConnection() was in unreachable dead code below the "while (true)" loop
    ///     -> connections acquired were NEVER returned to the pool. After `connectionNumber` calls,
    ///     SemaphoreSlim would completely run out of permits -> all subsequent requests would hang/timeout
    ///     indefinitely even if the DB was healthy (connection leak).
    ///  3) Only caught DbException when retrying to acquire a connection -> other transient errors thrown from lower layers
    ///     (TimeoutException, InvalidOperationException due to zombie connection) would break the retry loop
    ///     instead of allowing further retries.
    ///
    /// Also implements EXECUTION-LEVEL failure classification, which is a different problem from
    /// acquiring a connection in the first place (that part is entirely handled inside
    /// AcquireConnectionAsync, using the pool's SemaphoreSlim). Once we already HOLD a connection and
    /// call action() on it, a failure can mean one of two very different things, and treating them
    /// the same way is wrong:
    ///
    ///  A) The connection is still alive (connection.Driver.IsValidSession == true) but the command
    ///     itself failed - bad SQL, a constraint violation, wrong parameters, a business-rule
    ///     exception thrown by the driver, etc. This is "you wrote a bad query" - retrying it against
    ///     a new connection will just fail again with the exact same error, so it is thrown
    ///     immediately, with no retry.
    ///
    ///  B) The connection DIED while action() was running (network drop, DB restart/failover, "server
    ///     closed the connection unexpectedly", the session being killed by the DB, ...). This is a
    ///     genuine transient/system-level failure that has nothing to do with whether the query itself
    ///     was correct, so the whole action is retried against a brand-new connection, up to
    ///     MaxExecutionRetryAttempts times.
    ///
    ///  A failure is only classified as case (B) - and therefore retried - when BOTH of these are true:
    ///    - the exception is a System.Data.Common.DbException (a DB-layer failure, not an application
    ///      bug such as a NullReferenceException in the caller's command-building code - those must
    ///      never be retried, since retrying application bugs is pointless and, for a write, can cause
    ///      duplicate side effects), AND
    ///    - connection.Driver.IsValidSession is now false (the connection genuinely died - this is not
    ///      just a query returning an error while the session stays healthy, and it is not just a
    ///      command timing out on an otherwise-healthy session, which is intentionally treated as
    ///      case (A) since retrying an already slow/overloaded query is not obviously safe).
    ///
    ///  NOTE on write safety: because we cannot know for certain whether a write command had already
    ///  been applied on the server before the connection dropped (the ack could have been lost after a
    ///  successful commit), retrying here is inherently "at-least-once", not "exactly-once". Keeping
    ///  MaxExecutionRetryAttempts small (default: 1 retry) and making writes idempotent where possible
    ///  (e.g. via a natural key / upsert) is the safest way to rely on this.
    /// </summary>
    private async Task<TResult?> ExecuteWithConnectionAsync<TResult>(
        Func<NpOnDbConnection, Task<TResult>> action) where TResult : class
    {
        var factory = Factory ?? throw new InvalidOperationException("Database Factory has not been initialized.");

        for (int attempt = 1; attempt <= MaxExecutionRetryAttempts; attempt++)
        {
            var connection = await AcquireConnectionAsync(factory).ConfigureAwait(false);

            try
            {
                return await action(connection).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Case (B) from the summary above: the connection genuinely died mid-execution.
                bool isSystemLevelFailure = ex is System.Data.Common.DbException && !connection.Driver.IsValidSession;
                bool canRetry = isSystemLevelFailure && attempt < MaxExecutionRetryAttempts;

                if (!canRetry)
                {
                    // Either case (A) - the connection is fine, this is a real query error, retrying
                    // would just reproduce the same failure - or we already used up our retry budget.
                    _logger?.LogError(ex, isSystemLevelFailure
                        ? "BaseDbFactoryWrapper: Connection died mid-execution and retry attempts are exhausted."
                        : "BaseDbFactoryWrapper: Error executing DB command.");
                    throw;
                }

                _logger?.LogWarning(ex,
                    $"BaseDbFactoryWrapper: Connection died mid-execution (attempt {attempt}/{MaxExecutionRetryAttempts}), retrying with a fresh connection...");
            }
            finally
            {
                // ALWAYS return the connection to the pool, whether action() succeeded, failed with a
                // query error, or died mid-execution. A dead connection pushed back here is lazily
                // validated and self-healed by GetConnectionAsync() on the next acquisition (see
                // BaseDbDriverFactory), so the retry attempt above is guaranteed to get a different,
                // healthy connection rather than being handed this exact same broken one again.
                factory.ReleaseConnection(connection);
            }
        }

        // Unreachable in practice - the loop above always either returns a result or throws - kept
        // only to satisfy the compiler, since it cannot prove that from a bounded "for" loop.
        throw new InvalidOperationException("BaseDbFactoryWrapper: Unexpected retry loop exit.");
    }

    /// <summary>
    /// Loop to acquire a connection from the Factory, retrying with exponential backoff for up to
    /// factory.ConnectionTimeoutSessions() seconds. Retains the spirit of the original ("removed
    /// redundant polling because Factory's SemaphoreSlim automatically waits with timeout") but fixed
    /// it to actually compile and correctly retry when the Factory returns null or throws a transient
    /// exception.
    ///
    /// This only handles NOT HAVING a connection yet (pool exhausted / DB temporarily unreachable). It
    /// is a different concern from a connection dying AFTER it was successfully acquired and handed to
    /// action() - that case is handled by the retry loop in ExecuteWithConnectionAsync above.
    /// </summary>
    private async Task<NpOnDbConnection> AcquireConnectionAsync(IDbDriverFactory factory)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(factory.ConnectionTimeoutSessions()));
        var retryDelay = InitialRetryDelay;

        while (true)
        {
            try
            {
                var connection = await factory.GetConnectionAsync().ConfigureAwait(false);
                if (connection != null)
                    return connection; // Got a connection -> return / break immediately.
            }
            // Catch a broad set of transient exceptions when acquiring a connection (not just
            // DbException), because reopening a zombie connection can throw TimeoutException /
            // InvalidOperationException / ObjectDisposedException (e.g. when the pool is being Reset()
            // concurrently).
            catch (Exception ex) when (ex is System.Data.Common.DbException
                                           or TimeoutException
                                           or InvalidOperationException
                                           or ObjectDisposedException)
            {
                _logger?.LogDebug(ex, "DBConnection errored, prepare to retry...");
            }

            if (cts.IsCancellationRequested)
            {
                _logger?.LogWarning("Timeout or unable to acquire DbConnection from pool.");
                throw new TimeoutException("Timeout when trying to acquire a DB connection from the pool.");
            }

            try
            {
                await Task.Delay(retryDelay, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                _logger?.LogError(ex, "BaseDbFactoryWrapper: Error executing DB command.");
                throw new TimeoutException("Timeout when trying to acquire a DB connection from the pool.");
            }

            // Exponential backoff (x1.5 each time, max 4s): the first retry is very fast (500ms) to avoid
            // slowing down requests when the pool is only momentarily busy, but if the DB truly
            // experiences a prolonged issue, the wait time gradually increases to avoid adding more
            // pressure to a failing system.
            retryDelay = TimeSpan.FromMilliseconds(
                Math.Min(retryDelay.TotalMilliseconds * RetryBackoffMultiplier, MaxRetryDelay.TotalMilliseconds));
        }
    }

    public async Task<INpOnWrapperResult?> ExecuteAsync(IBaseNpOnDbCommand dbCommand)
    {
        return await ExecuteWithConnectionAsync(connection => connection.Driver.Execute(dbCommand));
    }

    public async Task<INpOnWrapperResult?> ExecuteAsync(string queryString, List<INpOnDbCommandParam> parameters)
    {
        return await ExecuteWithConnectionAsync(connection =>
        {
            INpOnDbCommand command = new NpOnDbCommand(DbType, queryString, parameters);
            return connection.Driver.Execute(command);
        });
    }

    public async Task<INpOnWrapperResult?> ExecuteFuncParams(string funcName, List<INpOnDbCommandParam>? parameters)
    {
        return await ExecuteWithConnectionAsync(connection =>
        {
            INpOnDbExecFuncCommand execFuncCommand = new NpOnDbExecFuncCommand(DbType, funcName, parameters);
            return connection.Driver.Execute(execFuncCommand);
        });
    }

    public async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>?> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands)
    {
        return await ExecuteWithConnectionAsync(connection => connection.Driver.ExecuteWithTransaction(commands));
    }
}