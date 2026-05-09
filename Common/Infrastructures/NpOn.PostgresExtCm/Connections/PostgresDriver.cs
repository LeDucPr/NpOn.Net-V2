using System.Data;
using Common.Extensions.NpOn.CommonDb.Connections;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonDb.DbTransactions;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
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
    protected NpgsqlConnection? Connection;
    protected readonly IObjectPool<PostgresResultSetWrapper>? ResultSetPool;

    public override string Name { get; set; } = "NpOn-V2.PostgresDriver";
    public override string Version { get; set; } = "1.0";

    public override bool IsValidSession => Connection is { State: ConnectionState.Open };

    public PostgresDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        // Get the pool for PostgresResultSetWrapper if store is provided.
        if (objectPoolStore != null)
        {
            ResultSetPool = objectPoolStore.GetPool(() => new PostgresResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession)
        {
            return; // Already connected.
        }

        // await DisconnectAsync();
        Connection ??= new NpgsqlConnection(Option.ConnectionString);
        await Connection.OpenAsync(cancellationToken);
        Version = Connection.PostgreSqlVersion.ToString();
        if (Connection.Host != null)
            Name = Connection.Host;
        else
            Name = $"PostgresSql {Connection.PostgreSqlVersion.Major}"; // ?????????????
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

        return new PostgresResultSetWrapper().SetFail(error);
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

        return new PostgresResultSetWrapper().SetFail(ex);
    }

    protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null,
        bool fetchKeyInfo = false)
    {
        try
        {
            await using var pgCommand = new NpgsqlCommand(commandText, Connection);
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

            return new PostgresResultSetWrapper(reader);
        }
        catch (Exception ex)
        {
            return CreateFailResult(ex);
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

    #endregion private
}