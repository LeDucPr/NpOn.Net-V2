using System.Data;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.NpOn.MssqlExtCm.Results;
using Microsoft.Data.SqlClient;

// Important: Use Microsoft.Data.SqlClient library

namespace Common.Infrastructures.NpOn.MssqlExtCm.Connections;

public class MssqlDriver : NpOnDbDriver
{
    private SqlConnection? _connection;
    public sealed override string Name { get; set; }
    public sealed override string Version { get; set; }

    public override bool IsValidSession => _connection is { State: ConnectionState.Open };

    public MssqlDriver(INpOnConnectOption option) : base(option)
    {
        Name = "Mssql";
        Version = "0.0";
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return; // Already connected.
        }

        await DisconnectAsync();
        _connection = new SqlConnection(Option.ConnectionString);
        await _connection.OpenAsync(cancellationToken);
        Version = _connection.ServerVersion ?? Version;
        if (!string.IsNullOrEmpty(_connection.DataSource))
            Name = _connection.DataSource;
        else
            Name = $"MSSQL Server {Version.Split('.')[0]}";
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
        // Check for a valid connection state.
        if (!IsValidSession || _connection == null)
            return new MssqlResultSetWrapper().SetFail(EDbError.Connection);
        if (command is not INpOnDbCommand execCommand)
            return new MssqlResultSetWrapper().SetFail(EDbError.Command);
        if (string.IsNullOrWhiteSpace(execCommand.CommandText))
            return new MssqlResultSetWrapper().SetFail(EDbError.Command);
        try
        {
            await using var sqlCommand = _connection.CreateCommand();
            sqlCommand.CommandText = execCommand.CommandText;
            await using var reader = await sqlCommand.ExecuteReaderAsync();
            return new MssqlResultSetWrapper(reader);
        }
        catch (Exception ex)
        {
            return new MssqlResultSetWrapper().SetFail(ex);
        }
    }
}