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
            return; // Đã kết nối rồi và option yêu cầu chờ.
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
        if (!IsValidSession || _connection == null)
            return new PostgresResultSetWrapper().SetFail(EDbError.Connection);
        if (execCommand == null || string.IsNullOrWhiteSpace(execCommand.FuncName))
            return new PostgresResultSetWrapper().SetFail(EDbError.ExecFuncName);

        var parameters = new List<INpOnDbCommandParam>();
        var paramNames = new List<string>();

        foreach (var param in execCommand.Params)
        {
            var npgsqlDbType = NpgsqlDbType.Unknown;
            if (param.Value != DBNull.Value)
            {
                npgsqlDbType = param.Value.GetType().ToNpgsqlDbType() ?? NpgsqlDbType.Unknown;
            }

            var dbParam = new NpOnDbCommandParam<NpgsqlDbType>
            {
                ParamName = param.Key,
                ParamType = npgsqlDbType,
                ParamValue = param.Value
            };
            parameters.Add(dbParam);
            paramNames.Add($"@{param.Key}");
        }

        string commandText = $"SELECT * FROM {execCommand.FuncName}({string.Join(",", paramNames)})";
        if (!string.IsNullOrWhiteSpace(execCommand.AliasForSingleColumnOutput))
        {
            commandText += $" AS {execCommand.AliasForSingleColumnOutput}";
        }

        return await ExecuteReaderInternalAsync(commandText, parameters);
    }

    public override async Task<INpOnWrapperResult> ExecuteFuncParams<TEnum>(INpOnDbExecFuncCommand? execCommand,
        List<INpOnDbCommandParam<TEnum>> parameters)
    {
        if (typeof(TEnum) != typeof(NpgsqlDbType))
        {
            return new PostgresResultSetWrapper().SetFail(new Exception($"{typeof(TEnum).Name} is not NpgsqlDbType"));
        }

        if (!IsValidSession || _connection == null) // Check enabled connection 
            return new PostgresResultSetWrapper().SetFail(EDbError.Connection);
        if (execCommand == null || string.IsNullOrWhiteSpace(execCommand.FuncName))
            return new PostgresResultSetWrapper().SetFail(EDbError.ExecFuncName);

        var paramNames = parameters.Select(p => $"@{p.ParamName}").ToList();
        string funcName = execCommand.FuncName.Trim();
        string commandText = $"SELECT * FROM {funcName}({string.Join(",", paramNames)})";

        return await ExecuteReaderInternalAsync(commandText, parameters);
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
                    if (prm is not NpOnDbCommandParam<NpgsqlDbType> npgsqlParam)
                    {
                        // Fallback for basic parameters if type is not specified
                        var basicParam = new NpgsqlParameter(prm.ParamName, prm.ParamValue ?? DBNull.Value);
                        pgCommand.Parameters.Add(basicParam);
                        continue;
                    }

                    pgCommand.Parameters.Add(npgsqlParam.CreateNpgsqlParameter());
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