using System.Data;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbTransactions;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using Npgsql;
using NpgsqlTypes;

namespace Common.Infrastructures.NpOn.PostgresExtCm.Connections;

public class PostgresDriver : NpOnDbDriver
{
    private NpgsqlConnection? _connection;
    public sealed override string Name { get; set; }
    public sealed override string Version { get; set; }

    public override bool IsValidSession => _connection is { State: ConnectionState.Open };

    public PostgresDriver(INpOnConnectOption option) : base(option)
    {
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return; // Already connected.
        }

        await DisconnectAsync();
        _connection = new NpgsqlConnection(Option.ConnectionString);
        await _connection.OpenAsync(cancellationToken);
        Version = _connection.PostgreSqlVersion.ToString();
        if (_connection.Host != null)
            Name = _connection.Host;
        else
            Name = $"PostgresSql {_connection.PostgreSqlVersion.Major}"; // ?????????????
    }

    public override async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        if (!IsValidSession || _connection == null)
        {
            throw new InvalidOperationException("Connection is not open.");
        }

        var npgsqlTransaction = await _connection.BeginTransactionAsync(cancellationToken);
        return new NpOnDbTransaction(npgsqlTransaction);
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || _connection == null)
            return new PostgresResultSetWrapper().SetFail(EDbError.Connection);
        var commandBuilder = CommandCustomBuilder(command);
        if (command == null || string.IsNullOrWhiteSpace(commandBuilder.CommandText))
            return new PostgresResultSetWrapper().SetFail(EDbError.Command);

        return await ExecuteReaderInternalAsync(commandBuilder.CommandText, commandBuilder.Parameters);
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
                        (commandBuilder.CommandText, commandBuilder.Parameters, transaction);
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

    private async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null)
    {
        try
        {
            await using var pgCommand = new NpgsqlCommand(commandText, _connection);
            if (transaction?.DbTransaction is NpgsqlTransaction dbTransaction) // use transaction 
            {
                pgCommand.Transaction = dbTransaction;
            }

            if (parameters != null)
            {
                foreach (var prm in parameters)
                {
                    var pgParam = new NpgsqlParameter { ParameterName = prm.ParamName };

                    var targetDbType = NpgsqlDbType.Unknown;
                    if (prm is NpOnDbCommandParam<NpgsqlDbType> typedParam)
                        targetDbType = typedParam.ParamType;

                    var adoNetValue = PostgresUtils.ConvertStringToNpgsqlType(prm.ParamValue, targetDbType);

                    if (targetDbType != NpgsqlDbType.Unknown)
                        pgParam.NpgsqlDbType = targetDbType;

                    pgParam.Value = adoNetValue ?? DBNull.Value;
                    pgCommand.Parameters.Add(pgParam);
                }
            }

            // Note: If using a Transaction, CommandBehavior.Default is typically used 
            // because the Transaction manages the connection lifecycle.
            await using var reader = await pgCommand.ExecuteReaderAsync();
            return new PostgresResultSetWrapper(reader);
        }
        catch (Exception ex)
        {
            return new PostgresResultSetWrapper().SetFail(ex);
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

    #endregion private
}