using System.Data;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.CommonDb.DbTransactions;
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

    public override async Task<INpOnDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException("Connection is not open.");
        }

        var npgsqlTransaction = await _connection.BeginTransactionAsync(cancellationToken);
        return new NpOnDbTransaction(npgsqlTransaction);
    }

    public override async Task<INpOnWrapperResult> Execute(INpOnDbCommand? command)
    {
        if (!IsValidSession || _connection == null)
            return new PostgresResultSetWrapper().SetFail(EDbError.Connection);
        if (command == null || string.IsNullOrWhiteSpace(command.CommandText))
            return new PostgresResultSetWrapper().SetFail(EDbError.Command);

        return await ExecuteReaderInternalAsync(command.CommandText, command.Parameters);
    }

    public override async Task<INpOnWrapperResult> ExecuteFunc(INpOnDbExecFuncCommand? execCommand)
    {
        if (!IsValidSession || _connection == null) // Check enabled connection 
            return new PostgresResultSetWrapper().SetFail(EDbError.Connection);
        if (execCommand == null || string.IsNullOrWhiteSpace(execCommand.FuncName))
            return new PostgresResultSetWrapper().SetFail(EDbError.ExecFuncName);

        List<string>? paramNames = execCommand.Parameters?.Select(p => $"@{p.ParamName}").ToList();
        string paramNamesJoin = (paramNames != null && paramNames.Any())
            ? string.Join(",", paramNames)
            : string.Empty;
        string funcName = execCommand.FuncName.Trim();
        string commandText = $"SELECT * FROM {funcName}({paramNamesJoin})";

        return await ExecuteReaderInternalAsync(commandText, execCommand.Parameters);
    }


    #region private

    private async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters)
    {
        try
        {
            await using var pgCommand = new NpgsqlCommand(commandText, _connection);
            if (parameters != null)
            {
                foreach (var prm in parameters)
                {
                    var pgParam = new NpgsqlParameter
                    {
                        ParameterName = prm.ParamName
                    };

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

            await using var reader = await pgCommand.ExecuteReaderAsync();
            return new PostgresResultSetWrapper(reader);
        }
        catch (Exception ex)
        {
            return new PostgresResultSetWrapper().SetFail(ex);
        }
    }

    #endregion private
}