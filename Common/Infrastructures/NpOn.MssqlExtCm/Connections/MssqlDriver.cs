using System.Data;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbTransactions;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.MssqlExtCm.Results;
using Microsoft.Data.SqlClient;

namespace Common.Infrastructures.NpOn.MssqlExtCm.Connections;

public class MssqlDriver : NpOnDbDriver
{
    protected SqlConnection? _connection;
    protected readonly IObjectPool<MssqlResultSetWrapper>? _resultSetPool;

    public sealed override string Name { get; set; } = "NpOn-V2.MssqlDriver";
    public sealed override string Version { get; set; } = "1.0";

    public override bool IsValidSession => _connection is { State: ConnectionState.Open };

    public MssqlDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            _resultSetPool = objectPoolStore.GetPool(() => new MssqlResultSetWrapper());

        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        await DisconnectAsync();
        _connection = new SqlConnection(Option.ConnectionString);
        await _connection.OpenAsync(cancellationToken);
        
        Version = _connection.ServerVersion.AsEmptyString();
        Name = $"MSSQL Server {_connection.DataSource}";
    }

    public override async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _connection == null)
            return CreateFailResult(EDbError.Connection);

        var (commandText, parameters) = CommandCustomBuilder(command);
        if (command == null || string.IsNullOrWhiteSpace(commandText))
            return CreateFailResult(EDbError.Command);

        return await ExecuteReaderInternalAsync(commandText, parameters, null, command.IsFetchKeyInfo);
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
                var (commandText, parameters) = CommandCustomBuilder(command);
                if (string.IsNullOrWhiteSpace(commandText))
                {
                    results.Add(command, CreateFailResult(EDbError.Command));
                    continue;
                }

                var res = await ExecuteReaderInternalAsync(commandText, parameters, transaction, command.IsFetchKeyInfo);
                results.Add(command, res);
            }
            return results;
        }, cancellationToken);
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        if (!IsValidSession) await ConnectAsync(cancellationToken);
        if (_connection == null) throw new InvalidOperationException("Could not open connection for transaction");
        
        var sqlTransaction = (SqlTransaction)await _connection.BeginTransactionAsync(cancellationToken);
        return new NpOnDbTransaction(sqlTransaction);
    }

    #region protected helpers

    protected INpOnWrapperResult CreateFailResult(EDbError error)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(error);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }
        return new MssqlResultSetWrapper().SetFail(error);
    }

    protected INpOnWrapperResult CreateFailResult(Exception ex)
    {
        if (_resultSetPool != null)
        {
            var wrapper = _resultSetPool.Get();
            wrapper.Reset();
            wrapper.SetFail(ex);
            wrapper.ReturnToPool = w => _resultSetPool.Return(w);
            return wrapper;
        }
        return new MssqlResultSetWrapper().SetFail(ex);
    }

    protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null,
        bool fetchKeyInfo = false)
    {
        if (!IsValidSession && transaction == null) await ConnectAsync(default);
        if (!IsValidSession && transaction == null) return CreateFailResult(EDbError.Connection);
        if (string.IsNullOrWhiteSpace(commandText)) return CreateFailResult(EDbError.Command);

        try
        {
            using var sqlCommand = _connection!.CreateCommand();
            sqlCommand.CommandText = commandText;
            
            if (transaction != null)
                sqlCommand.Transaction = (SqlTransaction)transaction.DbTransaction;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var sqlParam = new SqlParameter(param.ParamName, param.ParamValue ?? DBNull.Value);
                    if (param is NpOnDbCommandParam<SqlDbType> typedParam)
                        sqlParam.SqlDbType = typedParam.ParamType;
                    sqlCommand.Parameters.Add(sqlParam);
                }
            }

            CommandBehavior behavior = fetchKeyInfo ? CommandBehavior.KeyInfo : CommandBehavior.Default;
            using var reader = await sqlCommand.ExecuteReaderAsync(behavior);
            
            MssqlResultSetWrapper resultSet;
            if (_resultSetPool != null)
            {
                resultSet = _resultSetPool.Get();
                resultSet.Reset();
                resultSet.ReturnToPool = w => _resultSetPool.Return(w);
            }
            else
            {
                resultSet = new MssqlResultSetWrapper();
            }

            resultSet.Init(reader);
            return resultSet;
        }
        catch (Exception ex)
        {
            return CreateFailResult(ex);
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
                // MSSQL stored procedure call syntax or EXEC function
                string commandText = $"EXEC {funcName} {paramNamesJoin}";
                return (commandText, execFuncCommand.Parameters);
            }
            default:
                return (string.Empty, null);
        }
    }

    #endregion
}