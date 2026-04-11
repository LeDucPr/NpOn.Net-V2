using System.Data;
using ClickHouse.Client.ADO;
using ClickHouse.Client.ADO.Parameters;
using ClickHouse.Client.ADO.Readers;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.ICommonDb.Connections;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.Transactions;
using Common.Infrastructures.NpOn.ClickHouseExtCm.Results;

namespace Common.Infrastructures.NpOn.ClickHouseExtCm.Connections;

public class ClickHouseDriver : NpOnDbDriver
{
    protected ClickHouseConnection? _connection;
    protected readonly IObjectPool<ClickHouseResultSetWrapper>? _resultSetPool;

    public sealed override string Name { get; set; } = "NpOn-V2.ClickHouseDriver";
    public sealed override string Version { get; set; } = "1.0";

    public override bool IsValidSession => _connection is { State: ConnectionState.Open };

    public ClickHouseDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            _resultSetPool = objectPoolStore.GetPool(() => new ClickHouseResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        await DisconnectAsync();
        _connection = new ClickHouseConnection(Option.ConnectionString);
        await _connection.OpenAsync(cancellationToken);

        Version = _connection.ServerVersion.AsEmptyString();
        Name = $"ClickHouse Server {_connection.DataSource}";
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

    public override Task<Dictionary<IBaseNpOnDbCommand, INpOnWrapperResult>> ExecuteWithTransaction(
        IEnumerable<IBaseNpOnDbCommand> commands,
        CancellationToken cancellationToken = default)
    {
        // ClickHouse does not support ACID transactions.
        throw new NotSupportedException("ClickHouse does not support standard ACID transactions.");
    }

    protected override Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("ClickHouse does not support standard ACID transactions.");
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
        return new ClickHouseResultSetWrapper().SetFail(error);
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
        return new ClickHouseResultSetWrapper().SetFail(ex);
    }

    protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null,
        bool fetchKeyInfo = false)
    {
        if (!IsValidSession) await ConnectAsync(default);
        if (!IsValidSession) return CreateFailResult(EDbError.Connection);

        try
        {
            using var chCommand = _connection!.CreateCommand();
            chCommand.CommandText = commandText;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var chParam = new ClickHouseDbParameter { ParameterName = param.ParamName };
                    
                    EClickHouseDbType? targetChType = null;
                    if (param is NpOnDbCommandParam<EClickHouseDbType> typedParam)
                        targetChType = typedParam.ParamType;

                    var adoNetValue = ClickHouseUtils.ConvertToClickHouseType(param.ParamValue, targetChType);

                    if (targetChType.HasValue && targetChType != EClickHouseDbType.Unknown)
                        chParam.ClickHouseType = targetChType.ToString();

                    chParam.Value = adoNetValue ?? DBNull.Value;
                    chCommand.Parameters.Add(chParam);
                }
            }

            // ClickHouse doesn't use CommandBehavior the same way as SQL Server (KeyInfo etc.)
            // but we use ExecuteReaderAsync to get a stream.
            using var reader = (ClickHouseDataReader)await chCommand.ExecuteReaderAsync();

            ClickHouseResultSetWrapper resultSet;
            if (_resultSetPool != null)
            {
                resultSet = _resultSetPool.Get();
                resultSet.Reset();
                resultSet.ReturnToPool = w => _resultSetPool.Return(w);
            }
            else
            {
                resultSet = new ClickHouseResultSetWrapper();
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
                // ClickHouse function call syntax: SELECT funcName(args)
                string commandText = $"SELECT {funcName}({paramNamesJoin})";
                return (commandText, execFuncCommand.Parameters);
            }
            default:
                return (string.Empty, null);
        }
    }

    #endregion
}
