using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
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

    private static string? _cachedVersion;
    private static string? _cachedName;

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

        Connection ??= new NpgsqlConnection(Option.ConnectionString);
        await Connection.OpenAsync(cancellationToken);
        Version = _cachedVersion ??= Connection.PostgreSqlVersion.ToString();
        Name = _cachedName ??= (Connection.Host ?? $"PostgresSql {Connection.PostgreSqlVersion.Major}");
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

    public async Task<INpOnWrapperResult> BinaryCopyAsync(string tableName, IReadOnlyList<string> columns, IReadOnlyList<object[]> rows)
    {
        if (!IsValidSession || Connection == null)
            return CreateFailResult(EDbError.Connection);

        try
        {
            var quotedColumns = string.Join(", ", columns.Select(c => $"\"{c}\""));
            var copySql = $"COPY {tableName} ({quotedColumns}) FROM STDIN (FORMAT BINARY)";
            await using var writer = await Connection.BeginBinaryImportAsync(copySql);

            for (int r = 0; r < rows.Count; r++)
            {
                await writer.StartRowAsync();
                var rowData = rows[r];
                for (int c = 0; c < rowData.Length; c++)
                {
                    var val = rowData[c];
                    if (val == null || val is DBNull)
                        await writer.WriteNullAsync();
                    else
                        await writer.WriteAsync(val);
                }
            }
            await writer.CompleteAsync();

            if (ResultSetPool != null)
            {
                var fastWrapper = ResultSetPool.Get();
                fastWrapper.Reset();
                fastWrapper.SetSuccess();
                fastWrapper.ReturnToPool = w => ResultSetPool.Return(w);
                return fastWrapper;
            }

            var result = new PostgresResultSetWrapper();
            result.SetSuccess();
            return result;
        }
        catch (Exception ex)
        {
            return CreateFailResult(ex);
        }
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

    private static bool IsPureNonQuery(string sql)
    {
        var trimmed = sql.AsSpan().TrimStart();
        if (trimmed.Length < 6) return false;

        bool isWrite = trimmed.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("MERGE", StringComparison.OrdinalIgnoreCase);

        if (!isWrite) return false;

        // If it contains RETURNING, caller expects a resultset so it must use ExecuteReader
        return sql.IndexOf("RETURNING", StringComparison.OrdinalIgnoreCase) < 0;
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
                    if (prm is NpgsqlParameter directParam)
                    {
                        pgCommand.Parameters.Add(directParam);
                        continue;
                    }

                    var targetDbType = NpgsqlDbType.Unknown;
                    if (prm is NpOnDbCommandParam<NpgsqlDbType> typedParam)
                        targetDbType = typedParam.ParamType;

                    var adoNetValue = PostgresUtils.ConvertStringToNpgsqlType(prm.ParamValue, targetDbType);
                    var pgParam = new NpgsqlParameter(prm.ParamName, adoNetValue ?? DBNull.Value);
                    if (targetDbType != NpgsqlDbType.Unknown)
                        pgParam.NpgsqlDbType = targetDbType;

                    pgCommand.Parameters.Add(pgParam);
                }
            }

            // FAST-PATH FOR WRITE (CRUD): ExecuteNonQueryAsync completely bypasses DataReader/schema allocations
            if (IsPureNonQuery(commandText))
            {
                await pgCommand.ExecuteNonQueryAsync();
                if (ResultSetPool != null)
                {
                    var fastWrapper = ResultSetPool.Get();
                    fastWrapper.Reset();
                    fastWrapper.SetSuccess();
                    fastWrapper.ReturnToPool = w => ResultSetPool.Return(w);
                    return fastWrapper;
                }
                var newFastWrapper = new PostgresResultSetWrapper();
                newFastWrapper.SetSuccess();
                return newFastWrapper;
            }

            // Transaction (using)
            CommandBehavior commandBehavior = fetchKeyInfo ? CommandBehavior.KeyInfo : CommandBehavior.Default;
            // not ex will be reset timeout of connection
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
        catch (NpgsqlException ex) when (ex.IsTransient)
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


    // protected async Task<INpOnWrapperResult> ExecuteReaderInternalAsync(
    //     string commandText,
    //     IEnumerable<INpOnDbCommandParam>? parameters,
    //     INpOnDbTransaction? transaction = null,
    //     bool fetchKeyInfo = false)
    // {
    //     try
    //     {
    //         await using var pgCommand = new NpgsqlCommand(commandText, Connection);
    //         if (transaction?.DbTransaction is NpgsqlTransaction dbTransaction) // use transaction 
    //         {
    //             pgCommand.Transaction = dbTransaction;
    //         }
    //
    //         if (parameters != null)
    //         {
    //             foreach (var prm in parameters)
    //             {
    //                 var pgParam = new NpgsqlParameter { ParameterName = prm.ParamName };
    //
    //                 var targetDbType = NpgsqlDbType.Unknown;
    //                 if (prm is NpOnDbCommandParam<NpgsqlDbType> typedParam)
    //                     targetDbType = typedParam.ParamType;
    //
    //                 var adoNetValue = PostgresUtils.ConvertStringToNpgsqlType(prm.ParamValue, targetDbType);
    //
    //                 if (targetDbType != NpgsqlDbType.Unknown)
    //                     pgParam.NpgsqlDbType = targetDbType;
    //
    //                 pgParam.Value = adoNetValue ?? DBNull.Value;
    //                 pgCommand.Parameters.Add(pgParam);
    //             }
    //         }
    //
    //         // Transaction (using)
    //         CommandBehavior commandBehavior = fetchKeyInfo ? CommandBehavior.KeyInfo : CommandBehavior.Default;
    //         await using var reader = await pgCommand.ExecuteReaderAsync(commandBehavior);
    //
    //         if (ResultSetPool != null)
    //         {
    //             var wrapper = ResultSetPool.Get();
    //             wrapper.Reset();
    //             wrapper.Init(reader);
    //             wrapper.ReturnToPool = w => ResultSetPool.Return(w); // Set return action
    //             return wrapper;
    //         }
    //
    //         return new PostgresResultSetWrapper(reader);
    //    

    #region Direct Streaming Query (Zero Intermediate Buffer)

    private static readonly ConcurrentDictionary<string, Delegate> StreamReaderCache = new();

    public override async Task<List<T>> QueryAsync<T>(IBaseNpOnDbCommand? command)
    {
        if (!IsValidSession || Connection == null)
            throw new InvalidOperationException("Postgres connection is not valid or closed.");

        var commandBuilder = CommandCustomBuilder(command);
        await using var npgsqlCommand = new NpgsqlCommand(commandBuilder.CommandText, Connection);

        if (commandBuilder.Parameters is { Count: > 0 })
        {
            for (int i = 0; i < commandBuilder.Parameters.Count; i++)
            {
                var p = commandBuilder.Parameters[i];
                npgsqlCommand.Parameters.Add(new NpgsqlParameter(p.ParamName, p.ParamValue ?? DBNull.Value));
            }
        }

        try
        {
            await using var reader = await npgsqlCommand.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess | CommandBehavior.SingleResult).ConfigureAwait(false);

            if (!reader.HasRows) return new List<T>(0);

            var streamReader = GetOrCreateStreamReader<T>(reader);
            return streamReader(reader);
        }
        catch (NpgsqlException ex) when (ex.IsTransient)
        {
            throw;
        }
        finally
        {
            ResetSessionTimeout();
        }
    }

    private static Func<NpgsqlDataReader, List<T>> GetOrCreateStreamReader<T>(NpgsqlDataReader reader) where T : class
    {
        var targetType = typeof(T);
        var fieldCount = reader.FieldCount;
        var keyParts = new string[fieldCount];
        for (int i = 0; i < fieldCount; i++) keyParts[i] = reader.GetName(i);
        var schemaKey = $"{targetType.FullName}#Stream#{fieldCount}:" + string.Join(";", keyParts);

        return (Func<NpgsqlDataReader, List<T>>)StreamReaderCache.GetOrAdd(schemaKey, _ => CreateStreamReader<T>(reader));
    }

    private static Func<NpgsqlDataReader, List<T>> CreateStreamReader<T>(NpgsqlDataReader reader) where T : class
    {
        var targetType = typeof(T);
        var fieldCount = reader.FieldCount;

        var dynamicMethod = new DynamicMethod(
            $"NpgsqlStream_{targetType.Name}_{Guid.NewGuid():N}",
            typeof(List<T>),
            [typeof(NpgsqlDataReader)],
            typeof(PostgresDriver).Module,
            true);

        var il = dynamicMethod.GetILGenerator();

        var listType = typeof(List<T>);
        var listCtor = listType.GetConstructor(Type.EmptyTypes)!;
        var listAddMethod = listType.GetMethod(nameof(List<T>.Add), [targetType])!;
        var targetCtor = targetType.GetConstructor(Type.EmptyTypes) ??
            targetType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).FirstOrDefault() ??
            throw new InvalidOperationException($"Type {targetType.Name} must have an accessible constructor.");

        var listLocal = il.DeclareLocal(listType);
        var itemLocal = il.DeclareLocal(targetType);

        // List<T> list = new List<T>();
        il.Emit(OpCodes.Newobj, listCtor);
        il.Emit(OpCodes.Stloc, listLocal);

        // Resolve field map if available on type
        Dictionary<string, string>? customFieldMap = null;
        try
        {
            var tempInstance = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(targetType);
            var fieldMapProp = targetType.GetProperty("FieldMap", BindingFlags.Public | BindingFlags.Instance);
            if (fieldMapProp?.GetValue(tempInstance) is Dictionary<string, string> map && map.Count > 0)
                customFieldMap = map;
            else if (targetType.GetMethod("CreateDefaultFieldMapper") != null)
            {
                targetType.GetMethod("CreateDefaultFieldMapper")!.Invoke(tempInstance, null);
                if (fieldMapProp?.GetValue(tempInstance) is Dictionary<string, string> map2 && map2.Count > 0)
                    customFieldMap = map2;
            }
        }
        catch { }

        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .ToList();

        var colToProp = new (int colIndex, PropertyInfo? propInfo, Type colType)[fieldCount];
        for (int i = 0; i < fieldCount; i++)
        {
            var colName = reader.GetName(i);
            var colType = reader.GetFieldType(i);

            PropertyInfo? matchedProp = null;
            if (customFieldMap != null)
            {
                foreach (var kv in customFieldMap)
                {
                    if (string.Equals(kv.Value, colName, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedProp = properties.FirstOrDefault(p => p.Name == kv.Key);
                        if (matchedProp != null) break;
                    }
                }
            }

            matchedProp ??= properties.FirstOrDefault(p =>
                string.Equals(p.Name, colName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ToSnakeCase(p.Name), colName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Name, colName.Replace("_", ""), StringComparison.OrdinalIgnoreCase));

            colToProp[i] = (i, matchedProp, colType);
        }

        var loopStart = il.DefineLabel();
        var loopEnd = il.DefineLabel();

        // while (reader.Read())
        il.MarkLabel(loopStart);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.Read))!);
        il.Emit(OpCodes.Brfalse, loopEnd);

        // T item = new T();
        il.Emit(OpCodes.Newobj, targetCtor);
        il.Emit(OpCodes.Stloc, itemLocal);

        var isDbNullMethod = typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.IsDBNull), [typeof(int)])!;

        for (int c = 0; c < fieldCount; c++)
        {
            var (colIdx, propInfo, colType) = colToProp[c];
            if (propInfo == null || colIdx < 0) continue;

            var propType = propInfo.PropertyType;
            var underlyingType = Nullable.GetUnderlyingType(propType) ?? propType;
            var isNullable = Nullable.GetUnderlyingType(propType) != null;
            var skipColLabel = il.DefineLabel();

            // if (reader.IsDBNull(c)) goto skipColLabel;
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, c);
            il.Emit(OpCodes.Callvirt, isDbNullMethod);
            il.Emit(OpCodes.Brtrue, skipColLabel);

            // target for setter
            il.Emit(OpCodes.Ldloc, itemLocal);
            // reader for GetXxx
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4, c);

            if (underlyingType == typeof(Guid))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetGuid), [typeof(int)])!);
            }
            else if (underlyingType == typeof(string))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetString), [typeof(int)])!);
            }
            else if (underlyingType == typeof(DateTime))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetDateTime), [typeof(int)])!);
            }
            else if (underlyingType == typeof(bool))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetBoolean), [typeof(int)])!);
            }
            else if (underlyingType == typeof(long))
            {
                if (colType == typeof(long))
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt64), [typeof(int)])!);
                else if (colType == typeof(int))
                {
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt32), [typeof(int)])!);
                    il.Emit(OpCodes.Conv_I8);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt16), [typeof(int)])!);
                    il.Emit(OpCodes.Conv_I8);
                }
            }
            else if (underlyingType == typeof(int) || (underlyingType.IsEnum && Enum.GetUnderlyingType(underlyingType) == typeof(int)))
            {
                if (colType == typeof(int))
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt32), [typeof(int)])!);
                else if (colType == typeof(long))
                {
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt64), [typeof(int)])!);
                    il.Emit(OpCodes.Conv_I4);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt16), [typeof(int)])!);
                    il.Emit(OpCodes.Conv_I4);
                }
            }
            else if (underlyingType == typeof(short))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetInt16), [typeof(int)])!);
            }
            else if (underlyingType == typeof(double))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetDouble), [typeof(int)])!);
            }
            else if (underlyingType == typeof(decimal))
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetDecimal), [typeof(int)])!);
            }
            else
            {
                il.Emit(OpCodes.Callvirt, typeof(NpgsqlDataReader).GetMethod(nameof(NpgsqlDataReader.GetValue), [typeof(int)])!);
                il.Emit(OpCodes.Unbox_Any, underlyingType);
            }

            if (isNullable)
            {
                var nullableCtor = propType.GetConstructor([underlyingType])!;
                il.Emit(OpCodes.Newobj, nullableCtor);
            }

            il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod()!);
            il.MarkLabel(skipColLabel);
        }

        // list.Add(item);
        il.Emit(OpCodes.Ldloc, listLocal);
        il.Emit(OpCodes.Ldloc, itemLocal);
        il.Emit(OpCodes.Callvirt, listAddMethod);

        il.Emit(OpCodes.Br, loopStart);

        il.MarkLabel(loopEnd);
        il.Emit(OpCodes.Ldloc, listLocal);
        il.Emit(OpCodes.Ret);

        return (Func<NpgsqlDataReader, List<T>>)dynamicMethod.CreateDelegate(typeof(Func<NpgsqlDataReader, List<T>>));
    }

    private static string ToSnakeCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        var sb = new StringBuilder(str.Length + 5);
        for (int i = 0; i < str.Length; i++)
        {
            char c = str[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && str[i - 1] != '_') sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    #endregion Direct Streaming Query

    #endregion private
}