using System.Data;
using System.Net.Sockets;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbTransactions;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.MySqlExtCm.Results;
using MySqlConnector;

namespace Common.Infrastructures.NpOn.MySqlExtCm.Connections;

public class MySqlDriver : NpOnDbDriver
{
    protected MySqlConnection? Connection;
    protected readonly IObjectPool<MySqlResultSetWrapper>? ResultSetPool;

    public override string Name { get; set; } = "NpOn-V2.MySqlDriver";
    public override string Version { get; set; } = "1.0";

    public override bool IsValidSession => Connection is { State: ConnectionState.Open };

    public MySqlDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        // Get the pool for MySqlResultSetWrapper if store is provided.
        if (objectPoolStore != null)
        {
            ResultSetPool = objectPoolStore.GetPool(() => new MySqlResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return; // Already connected.
        }

        // await DisconnectAsync();
        Connection ??= new MySqlConnection(Option.ConnectionString);
        await Connection.OpenAsync(cancellationToken);
        Version = Connection.ServerVersion;
        Name = Connection.Database;
        // else
        //     Name = $"MySqlSql {_connection.ServerVersion}"; // ?????????????
    }

    public override async Task DisconnectAsync()
    {
        if (Connection != null)
        {
            await Connection.CloseAsync();
            await Connection.DisposeAsync();
            Connection = null;
        }
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        if (!IsValidSession || Connection == null)
        {
            throw new InvalidOperationException("Connection is not open.");
        }

        var npgsqlTransaction = await Connection.BeginTransactionAsync(cancellationToken);
        return new NpOnDbTransaction(npgsqlTransaction);
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || Connection == null)
        {
            return CreateFailResult(EDbError.Connection);
        }

        var commandBuilder = CommandCustomBuilder(command);
        if (command == null || string.IsNullOrWhiteSpace(commandBuilder.CommandText))
        {
            return CreateFailResult(EDbError.Command);
        }

        return await ExecuteReaderInternalAsync(commandBuilder.CommandText, commandBuilder.Parameters, null,
            command.IsFetchKeyInfo);
    }

    public override async Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        return await TransactionWrapper(async (transaction) =>
            {
                var results = new Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>();

                foreach (var command in commands)
                {
                    var commandBuilder = CommandCustomBuilder(command);
                    var result = await ExecuteReaderInternalAsync
                        (commandBuilder.CommandText, commandBuilder.Parameters, transaction, command.IsFetchKeyInfo);
                    results.Add(command, result); // dict
                    // If a command fails, break the loop immediately so the Wrapper can handle Rollback
                    if (!result.Status)
                    {
                        break;
                    }
                }

                return results;
            },
            cancellationToken);
    }


    #region private

    protected INpOnWrapperResult CreateFailResult(EDbError error)
    {
        if (ResultSetPool != null)
        {
            var wrapper = ResultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(error);
            wrapper.ReturnToPool = w => ResultSetPool.Return(w);
            return wrapper;
        }

        return new MySqlResultSetWrapper().SetFail(error);
    }

    protected INpOnWrapperResult CreateFailResult(Exception ex)
    {
        if (ResultSetPool != null)
        {
            var wrapper = ResultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(ex);
            wrapper.ReturnToPool = w => ResultSetPool.Return(w);
            return wrapper;
        }

        return new MySqlResultSetWrapper().SetFail(ex);
    }

    protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null,
        bool fetchKeyInfo = false)
    {
        try
        {
            await using var pgCommand = new MySqlCommand(commandText, Connection);
            if (transaction?.DbTransaction is MySqlTransaction dbTransaction) // use transaction 
            {
                pgCommand.Transaction = dbTransaction;
            }

            if (parameters != null)
            {
                foreach (var prm in parameters)
                {
                    var pgParam = new MySqlParameter { ParameterName = prm.ParamName };

                    var targetDbType = MySqlDbType.VarChar;
                    if (prm is NpOnDbCommandParam<MySqlDbType> typedParam)
                        targetDbType = typedParam.ParamType;

                    var adoNetValue = MySqlUtils.ConvertStringToMySqlConnectorType(prm.ParamValue, targetDbType);

                    if (targetDbType != MySqlDbType.VarChar)
                        pgParam.MySqlDbType = targetDbType;

                    pgParam.Value = adoNetValue ?? DBNull.Value;
                    pgCommand.Parameters.Add(pgParam);
                }
            }

            // Transaction (using)
            CommandBehavior commandBehavior = fetchKeyInfo ? CommandBehavior.KeyInfo : CommandBehavior.Default;
            await using var reader = await pgCommand.ExecuteReaderAsync(commandBehavior);

            if (ResultSetPool != null)
            {
                var wrapper = ResultSetPool.Get();
                wrapper.Reset();
                wrapper.Init(reader);
                wrapper.ReturnToPool = w => ResultSetPool.Return(w); // Set return action
                return wrapper;
            }

            ResetSessionTimeout();
            return new MySqlResultSetWrapper(reader);
        }
        catch (MySqlException ex) when (IsMySqlConnectionError(ex))
        {
            throw; // System.Data.Common.DbException
        }
        catch (Exception)
        {
            throw new ObjectDisposedException(""); 
            // return CreateFailResult(ex);
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    private static (string CommandText, List<INpOnDbCommandParam>? Parameters) CommandCustomBuilder(
        IBaseNpOnDbCommand? command)
    {
        switch (command)
        {
            case INpOnDbCommand execCommand:
                return (execCommand.CommandText, execCommand.Parameters);

            case INpOnDbExecFuncCommand execFuncCommand:
            {
                List<string>? paramNames = execFuncCommand.Parameters?.Select(p => $"@{p.ParamName}").ToList();
                string paramNamesJoin = (paramNames != null && paramNames.Any())
                    ? string.Join(",", paramNames)
                    : string.Empty;
                string funcName = execFuncCommand.FuncName.Trim();
                string commandText = $"SELECT * FROM {funcName}({paramNamesJoin})";
                return (commandText, execFuncCommand.Parameters);
            }
            default:
                return (string.Empty, null);
        }
    }
    
    private static bool IsMySqlConnectionError(MySqlException ex)
    {
        int code = ex.Number;
        return MySqlConnectionErrorCodes.Contains(code) 
               || MySqlClientErrorCodes.Contains(code)
               || ex.InnerException is SocketException
               || ex.InnerException is IOException;
    }

    // MySQL error codes liên quan đến mất kết nối
    private static readonly int[] MySqlConnectionErrorCodes =
    {
        1040, // Too many connections
        1042, // Can't get hostname for your address
        1043, // Bad handshake
        1045, // Access denied (wrong credentials)
        1049, // Unknown database
        1053, // Server shutdown in progress
        1077, // MySQL shutdown in progress
        1080, // Forcing close of thread
        1152, // Aborted connection
        1153, // Packet bigger than max_allowed_packet
        1154, // Read error from connection pipe
        1156, // Packets out of order
        1158, // Error reading communication packets
        1159, // Timeout reading communication packets
        1160, // Error writing communication packets
        1161, // Timeout writing communication packets
    };

    // Client error codes (prefix 2xxx)
    private static readonly int[] MySqlClientErrorCodes =
    {
        2002, // Can't connect through socket
        2003, // Can't connect to MySQL server on host
        2006, // MySQL server has gone away
        2013, // Lost connection during query
        2026, // SSL connection error
    };

    #endregion private
}