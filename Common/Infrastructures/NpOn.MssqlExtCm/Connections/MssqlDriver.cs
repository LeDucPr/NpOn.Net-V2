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
    protected SqlConnection? Connection;
    protected readonly IObjectPool<MssqlResultSetWrapper>? ResultSetPool;

    public sealed override string Name { get; set; } = "NpOn-V2.MssqlDriver";
    public sealed override string Version { get; set; } = "1.0";

    public override bool IsValidSession => Connection is { State: ConnectionState.Open };

    public MssqlDriver(INpOnConnectOption option, IObjectPoolStore? objectPoolStore = null) : base(option)
    {
        if (objectPoolStore != null)
        {
            ResultSetPool = objectPoolStore.GetPool(() => new MssqlResultSetWrapper());
        }
    }

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (IsValidSession) return;

        Connection = new SqlConnection(Option.ConnectionString);
        await Connection.OpenAsync(cancellationToken);

        Version = Connection.ServerVersion.AsEmptyString();
        Name = $"MSSQL Server {Connection.DataSource}";
    }

    public override async Task DisconnectAsync()
    {
        if (Connection != null)
        {
            await Connection.DisposeAsync();
            Connection = null;
        }
    }

    public override async Task<INpOnWrapperResult> Execute(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || Connection == null)
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

                var res = await ExecuteReaderInternalAsync(commandText, parameters, transaction,
                    command.IsFetchKeyInfo);
                results.Add(command, res);
            }

            return results;
        }, cancellationToken);
    }

    protected override async Task<INpOnDbTransaction> CreateTransaction(CancellationToken cancellationToken = default)
    {
        if (!IsValidSession) await ConnectAsync(cancellationToken);
        if (Connection == null) throw new InvalidOperationException("Could not open connection for transaction");

        var sqlTransaction = (SqlTransaction)await Connection.BeginTransactionAsync(cancellationToken);
        return new NpOnDbTransaction(sqlTransaction);
    }

    #region protected helpers

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

        return new MssqlResultSetWrapper().SetFail(error);
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

        return new MssqlResultSetWrapper().SetFail(ex);
    }

    protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
        string commandText,
        IEnumerable<INpOnDbCommandParam>? parameters,
        INpOnDbTransaction? transaction = null,
        bool fetchKeyInfo = false)
    {
        if (!IsValidSession && transaction == null) await ConnectAsync(CancellationToken.None);
        if (!IsValidSession && transaction == null) return CreateFailResult(EDbError.Connection);
        if (string.IsNullOrWhiteSpace(commandText)) return CreateFailResult(EDbError.Command);

        try
        {
            await using var sqlCommand = Connection!.CreateCommand();
            sqlCommand.CommandText = commandText;

            if (transaction != null)
                sqlCommand.Transaction = (SqlTransaction)transaction.DbTransaction;

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    var sqlParam = new SqlParameter { ParameterName = param.ParamName };

                    var targetSqlDbType = SqlDbType.Variant;
                    if (param is NpOnDbCommandParam<SqlDbType> typedParam)
                        targetSqlDbType = typedParam.ParamType;

                    var adoNetValue = MssqlUtils.ConvertStringToMssqlType(param.ParamValue, targetSqlDbType);

                    if (targetSqlDbType != SqlDbType.Variant)
                        sqlParam.SqlDbType = targetSqlDbType;

                    sqlParam.Value = adoNetValue ?? DBNull.Value;
                    sqlCommand.Parameters.Add(sqlParam);
                }
            }

            CommandBehavior behavior = fetchKeyInfo ? CommandBehavior.KeyInfo : CommandBehavior.Default;
            await using var reader = await sqlCommand.ExecuteReaderAsync(behavior);

            MssqlResultSetWrapper resultSet;
            if (ResultSetPool != null)
            {
                resultSet = ResultSetPool.Get();
                resultSet.Reset();
                resultSet.ReturnToPool = w => ResultSetPool.Return(w);
            }
            else
            {
                resultSet = new MssqlResultSetWrapper();
            }

            resultSet.Init(reader);
            return resultSet;
        }
        catch (SqlException ex) when (IsMssqlConnectionDead(ex))
        {
            throw; // System.Data.Common.DbException
        }
        catch (Exception ex)
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
                // MSSQL stored procedure call syntax or EXEC function
                string commandText = $"EXEC {funcName} {paramNamesJoin}";
                return (commandText, execFuncCommand.Parameters);
            }
            default:
                return (string.Empty, null);
        }
    }

    private static bool IsMssqlConnectionDead(SqlException ex)
    {
        // Characteristic Error Numbers for connection loss / timeout
        // -2: Timeout expired (Execute timeout)
        // 2, 53: Network-related or instance-specific error (Server not found)
        // 10053, 10054: Connection forcibly closed / Software caused connection abort
        // 10060: Connection timed out (Network timeout)
        // 10061: Connection refused (Server down or not listening on port)
        // 40xxx: Transient / connection drop errors commonly encountered on Azure SQL
        int[] deadConnectionErrors = { -2, 2, 53, 10053, 10054, 10060, 10061, 40143, 40197, 40501, 40613 };

        // Class >= 20 indicates severe errors that cause SQL Server to automatically close the current connection
        return deadConnectionErrors.Contains(ex.Number) || ex.Class >= 20;
    }

    #endregion
}