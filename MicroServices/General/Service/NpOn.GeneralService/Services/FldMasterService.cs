using System.Collections;
using System.Data;
using System.Text;
using Common.Applications.NpOn.CommonApplication.Services;
using Common.Extensions.NpOn.CommonBaseDomain;
using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums;
using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.CommonInternalCache.ObjectCachings;
using Common.Extensions.NpOn.CommonMode;
using Common.Extensions.NpOn.HandleFlow;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Extensions.NpOn.ICommonDb.DbResults.Grpc;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.DbFactories.NpOn.RedisFactory.Broadcasts;
using Common.Infrastructures.NpOn.RedisExtCm.Results;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using MicroServices.General.Definitions.NpOn.GeneralConstant;
using MicroServices.General.Service.NpOn.IGeneralService;
using MySqlConnector;
using Npgsql;
using NpgsqlTypes;

namespace MicroServices.General.Service.NpOn.GeneralService.Services
{
    public class FldMasterService(
        IWrapperCacheStore<string, List<TblFldRModel>> internalCache,
        IPostgresFactoryWrapper postgresFactoryWrapper,
        ILogger<CommonService> logger,
        IRedisBroadcastFactoryWrapper? redisBroadcastFactoryWrapper = null
    ) : CommonService(logger), IFldMasterService
    {
        private static readonly TimeSpan QueryCacheTtl = TimeSpan.FromSeconds(600);

        public static string BuildCacheKey(TblFldExecutionCommand executionCommand) =>
            !string.IsNullOrWhiteSpace(executionCommand.Code)
                ? $"code:{executionCommand.Code}"
                : $"id:{executionCommand.TblMaterId ?? string.Empty}";

        public async Task<CommonResponse> ExecuteDomainAction(DomainActionCommand command)
        {
            return await CommonProcess(async response =>
            {
                byte[]? body = command.Payload;
                if (body == null)
                {
                    response.SetFail("Payload not null.");
                    return;
                }

                Type domainType = command.DomainType;
                var domains = ProtoBufMode.ProtoBufDeserialize(body, domainType);
                List<BaseDomain> domainList;
                if (domains is IEnumerable enumerable)
                {
                    domainList = enumerable.Cast<BaseDomain>().ToList();
                }
                else if (domains is BaseDomain single)
                {
                    domainList = new List<BaseDomain> { single };
                }
                else
                {
                    response.SetFail("ProtoBufDeserialize Payload error.");
                    return;
                }

                (string commandText, IEnumerable<NpgsqlParameter> npgsqlParameters) = command.ActionType switch
                {
                    ERepositoryAction.Add => domainList.ToPostgresParamsInsert(),
                    ERepositoryAction.Update => domainList.ToPostgresParamsUpdate(),
                    ERepositoryAction.Delete => domainList.ToPostgresParamsDelete(),
                    ERepositoryAction.Merge => domainList.ToPostgresParamsMerge(),
                    _ => throw new ArgumentOutOfRangeException()
                };

                List<INpOnDbCommandParam> parameters = npgsqlParameters
                    .Select(p => (INpOnDbCommandParam)new NpOnDbCommandParam<NpgsqlDbType>(p.ParameterName, p.Value ?? DBNull.Value, p.NpgsqlDbType))
                    .ToList();

                INpOnDbCommand dbCommand =
                    new NpOnDbCommand(postgresFactoryWrapper.GetDbType(), commandText, parameters);
                INpOnWrapperResult? wrapperResult = await postgresFactoryWrapper.ExecuteAsync(dbCommand);
                if (wrapperResult == null || !wrapperResult.Status)
                {
                    response.SetFail($"Failed to {command.ActionType} domains.");
                    return;
                }

                response.SetSuccess();
            });
        }

        public async Task<CommonResponse<List<TblFldRModel>>> GetExecution(TblFldExecutionCommand executionCommand)
        {
            return await CommonProcess<List<TblFldRModel>>(async (response) =>
            {
                if (executionCommand.Code == null && executionCommand.TblMaterId == null)
                {
                    response.SetFail("Invalid query");
                    return;
                }

                List<INpOnDbCommandParam> parameters = new List<INpOnDbCommandParam>();
                var queryBuilder = new TblFldMasterQueryBuilder();
                if (executionCommand.Code != null)
                {
                    queryBuilder = queryBuilder.WhereCode(executionCommand.Code);
                    parameters.Add(new NpOnDbCommandParam<NpgsqlDbType>(nameof(executionCommand.Code), executionCommand.Code, NpgsqlDbType.Varchar));
                }
                else if (executionCommand.TblMaterId != null)
                {
                    queryBuilder = queryBuilder.WhereTblMasterId(executionCommand.TblMaterId);
                    parameters.Add(new NpOnDbCommandParam<NpgsqlDbType>(nameof(executionCommand.TblMaterId), executionCommand.TblMaterId, NpgsqlDbType.Uuid));
                }

                var (queryBuilderString, _) = queryBuilder.Build();

                INpOnWrapperResult? wrapperResult = await postgresFactoryWrapper.ExecuteAsync(queryBuilderString, parameters);
                if (wrapperResult == null)
                {
                    response.SetFail("FldMaster not found");
                    return;
                }

                List<TblFldRModel>? tblFldObjects = wrapperResult
                    .GenericConverter(typeof(TblFldRModel))?
                    .Cast<TblFldRModel>()
                    .ToList();

                if (tblFldObjects is not { Count: > 0 })
                {
                    response.SetFail("FldMasterObject not found");
                    return;
                }

                response.Data = tblFldObjects;
                response.SetSuccess();
            });
        }

        public async Task<CommonResponse<CommandRModel?>> GetExecCommand(TblFldExecutionCommand executionCommand)
        {
            return await CommonProcess<CommandRModel?>(async (response) =>
            {
                List<TblFldRModel> tblFldObjects;
                if (EApplicationConfiguration.IsUseCachingExecutionGeneralService.GetAppSettingConfig().AsDefaultBool())
                {
                    string cacheKey = BuildCacheKey(executionCommand);
                    tblFldObjects = await internalCache.GetOrAddAsync(
                        cacheKey,
                        async _ => (await GetExecution(executionCommand)).Data ?? [],
                        QueryCacheTtl);

                    // Do not keep negative/empty lookups pinned for 600s when the query has not been deployed yet.
                    if (tblFldObjects.Count == 0)
                    {
                        internalCache.Remove(cacheKey);
                    }
                }
                else
                {
                    tblFldObjects = (await GetExecution(executionCommand)).Data ?? [];
                }

                if (tblFldObjects is not { Count: > 0 })
                {
                    response.SetFail("FldMasterObject not found");
                    return;
                }

                TblFldRModel tblFldRModelFirst = tblFldObjects.First();
                if (string.IsNullOrWhiteSpace(tblFldRModelFirst.Query) &&
                    string.IsNullOrWhiteSpace(tblFldRModelFirst.ExecFunc))
                {
                    response.SetFail("Query null");
                    return;
                }

                if (tblFldRModelFirst.ExecType == null)
                {
                    response.SetFail("ExecType null");
                    return;
                }

                if (tblFldRModelFirst.DataBaseType == null)
                {
                    response.SetFail("DataBaseType null");
                    return;
                }

                EDb databaseType = (EDb)tblFldRModelFirst.DataBaseType;
                Type? deserializeParamType = databaseType switch
                {
                    EDb.Postgres or EDb.YugaBytePg => typeof(NpgsqlDbType),
                    EDb.ClickHouse => typeof(EClickHouseDbType),
                    EDb.MySql => typeof(MySqlDbType),
                    EDb.Mssql => typeof(SqlDbType),
                    EDb.Cassandra or EDb.ScyllaDb => typeof(ECassandraDbType),
                    EDb.Redis => typeof(ERedisCommand),
                    EDb.ElasticSearch => typeof(EElasticSearchCommand),
                    _ => null
                };

                List<NpOnDbCommandParamGrpc> parameters = [];
                foreach (var paramObj in tblFldObjects)
                {
                    if (string.IsNullOrEmpty(paramObj.FieldName))
                        break;

                    parameters.Add(new NpOnDbCommandParamGrpc
                    {
                        ParamName = paramObj.FieldName,
                        ParamType = (int?)(paramObj.FieldType ?? paramObj.FieldDbType)
                    });
                }

                EExecType execType = (EExecType)tblFldRModelFirst.ExecType;
                string commandText =
                    (execType == EExecType.Query ? tblFldRModelFirst.Query : tblFldRModelFirst.ExecFunc)!;
                var paramsList = new NpOnDbCommandParamGrpcList { Items = parameters };
                byte[] payload = ProtoBufMode.ProtoBufSerialize(paramsList);
                CommandRModel commandModel = new CommandRModel
                {
                    CommandText = commandText,
                    ParamsPayload = payload,
                    ExecType = execType,
                    DatabaseType = databaseType,
                    DeserializeParamType = deserializeParamType ?? typeof(NpOnDbCommandParam),
                };
                response.Data = commandModel;
                response.SetSuccess();
            });
        }

        public async Task<CommonResponse<INpOnGrpcObject>> Execute(TblFldExecutionCommand executionCommand)
        {
            return await CommonProcess<INpOnGrpcObject>(async (response) =>
            {
                List<TblFldRModel>? tblFldObjects = (await GetExecution(executionCommand)).Data;
                if (tblFldObjects is not { Count: > 0 })
                {
                    response.SetFail("FldMasterObject not found");
                    return;
                }

                TblFldRModel tblFldRModelFirst = tblFldObjects.First();
                INpOnWrapperResult? wrapperResult = null;
                if (tblFldRModelFirst is { ExecFunc: not null, ExecType: EExecType.ExecFunc })
                {
                    string funcName = tblFldRModelFirst.ExecFunc;
                    List<INpOnDbCommandParam> parameters = [];

                    foreach (var paramObj in tblFldObjects)
                    {
                        if (string.IsNullOrEmpty(paramObj.FieldName))
                            break;
                        parameters.Add(new NpOnDbCommandParam<NpgsqlDbType>(paramObj.FieldName, null, paramObj.FieldDbType ?? NpgsqlDbType.Unknown));
                    }

                    try
                    {
                        wrapperResult = await postgresFactoryWrapper.ExecuteFuncParams(
                            funcName, parameters);
                    }
                    catch (Exception)
                    {
                        response.SetFail("Execute Error!");
                        return;
                    }
                }
                else if (tblFldRModelFirst is { Query: not null, ExecType: EExecType.Query })
                {
                    string execString = tblFldRModelFirst.Query;
                    List<INpOnDbCommandParam> parameters = new List<INpOnDbCommandParam>();
                    foreach (var paramObj in tblFldObjects)
                    {
                        if (string.IsNullOrEmpty(paramObj.FieldName))
                            break;
                        parameters.Add(new NpOnDbCommandParam<NpgsqlDbType>(paramObj.FieldName, null, paramObj.FieldType ?? paramObj.FieldDbType ?? NpgsqlDbType.Unknown));
                    }

                    try
                    {
                        wrapperResult = await postgresFactoryWrapper.ExecuteAsync(execString, parameters);
                    }
                    catch (Exception)
                    {
                        response.SetFail("Query Error!");
                        return;
                    }
                }

                if (wrapperResult == null)
                {
                    response.SetFail("FldMaster not found");
                    return;
                }

                if (wrapperResult is not INpOnTableWrapper tableWrapperResult)
                {
                    response.SetFail("ValueFormat not found");
                    return;
                }

                if (tableWrapperResult.RowWrappers.Count == 0)
                    response.Data = null;
                else
                {
                    INpOnGrpcObject grpcObject = tableWrapperResult.ToGrpcTable();
                    response.Data = grpcObject;
                }

                response.SetSuccess();
            });
        }

        public async Task<CommonResponse<BulkSyncQueriesRModel?>> BulkSyncQueries(BulkSyncQueriesCommand command)
        {
            return await CommonProcess<BulkSyncQueriesRModel?>(async response =>
            {
                if (command.Queries is not { Count: > 0 })
                {
                    response.SetFail("Queries payload cannot be empty.");
                    return;
                }

                if (command.Queries.Any(q => string.IsNullOrWhiteSpace(q.Code)))
                {
                    response.SetFail("Every query item must define a non-empty Code.");
                    return;
                }

                var distinctCodes = command.Queries
                    .Select(q => q.Code)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (distinctCodes.Length != command.Queries.Count)
                {
                    response.SetFail("Duplicate query codes detected in BulkSyncQueries batch.");
                    return;
                }

                string deployVersion = string.IsNullOrWhiteSpace(command.DeployVersion)
                    ? IndexerMode.CreateShortGuid()
                    : command.DeployVersion;

                var dbType = postgresFactoryWrapper.GetDbType();
                var txCommands = new List<IBaseNpOnDbCommand>(command.Queries.Count * 3);
                var syntheticFallback = new Dictionary<string, List<TblFldRModel>>(StringComparer.Ordinal);

                foreach (var item in command.Queries)
                {
                    Guid tblId = Guid.TryParse(item.Id, out var parsedTblId) && parsedTblId != Guid.Empty
                        ? parsedTblId
                        : IndexerMode.CreateGuid();

                    const string upsertTblMasterSql = """
                        INSERT INTO public.tblmaster (id, code, description, execfunc, query, exectype, service_name, db_type)
                        VALUES (@id, @code, @description, @execfunc, @query, @exectype, @service_name, @db_type)
                        ON CONFLICT (code) DO UPDATE SET
                            description = EXCLUDED.description,
                            execfunc = EXCLUDED.execfunc,
                            query = EXCLUDED.query,
                            exectype = EXCLUDED.exectype,
                            service_name = EXCLUDED.service_name,
                            db_type = EXCLUDED.db_type;
                        """;

                    txCommands.Add(new NpOnDbCommand(dbType, upsertTblMasterSql,
                    [
                        new NpOnDbCommandParam<NpgsqlDbType>("id", tblId, NpgsqlDbType.Uuid),
                        new NpOnDbCommandParam<NpgsqlDbType>("code", item.Code, NpgsqlDbType.Varchar),
                        new NpOnDbCommandParam<NpgsqlDbType>("description", (object?)item.Description ?? DBNull.Value, NpgsqlDbType.Varchar),
                        new NpOnDbCommandParam<NpgsqlDbType>("execfunc", string.IsNullOrWhiteSpace(item.ExecFunc) ? DBNull.Value : item.ExecFunc, NpgsqlDbType.Varchar),
                        new NpOnDbCommandParam<NpgsqlDbType>("query", (object?)item.Query ?? DBNull.Value, NpgsqlDbType.Text),
                        new NpOnDbCommandParam<NpgsqlDbType>("exectype", item.ExecType, NpgsqlDbType.Integer),
                        new NpOnDbCommandParam<NpgsqlDbType>("service_name", (object?)item.ServiceName ?? DBNull.Value, NpgsqlDbType.Varchar),
                        new NpOnDbCommandParam<NpgsqlDbType>("db_type", item.DbType.HasValue ? item.DbType.Value : DBNull.Value, NpgsqlDbType.Integer),
                    ]));

                    const string deleteFieldsSql = """
                        DELETE FROM public.fld_query_master
                        WHERE tblmaster_id = (SELECT id FROM public.tblmaster WHERE code = @code);
                        """;

                    txCommands.Add(new NpOnDbCommand(dbType, deleteFieldsSql,
                    [
                        new NpOnDbCommandParam<NpgsqlDbType>("code", item.Code, NpgsqlDbType.Varchar),
                    ]));

                    var itemModels = new List<TblFldRModel>();
                    if (item.Fields is { Count: > 0 })
                    {
                        var insertSql = new StringBuilder(
                            "INSERT INTO public.fld_query_master (id, tblmaster_id, field_name, field_type, field_type_string, order_sort, description) " +
                            "SELECT v.id, t.id, v.field_name, v.field_type, v.field_type_string, v.order_sort, v.description " +
                            "FROM public.tblmaster t CROSS JOIN (VALUES ");

                        var fieldParams = new List<INpOnDbCommandParam>(1 + item.Fields.Count * 6)
                        {
                            new NpOnDbCommandParam<NpgsqlDbType>("code", item.Code, NpgsqlDbType.Varchar),
                        };

                        var orderedFields = item.Fields.OrderBy(f => f.OrderSort).ToList();
                        for (int idx = 0; idx < orderedFields.Count; idx++)
                        {
                            var f = orderedFields[idx];
                            Guid fldId = Guid.TryParse(f.Id, out var parsedFldId) && parsedFldId != Guid.Empty
                                ? parsedFldId
                                : IndexerMode.CreateGuid();

                            if (idx > 0) insertSql.Append(", ");
                            insertSql.Append($"(@f_id_{idx}, @f_name_{idx}, @f_type_{idx}, @f_type_str_{idx}, @f_sort_{idx}, @f_desc_{idx})");

                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_id_{idx}", fldId, NpgsqlDbType.Uuid));
                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_name_{idx}", f.FieldName, NpgsqlDbType.Varchar));
                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_type_{idx}", f.FieldType.HasValue ? f.FieldType.Value : DBNull.Value, NpgsqlDbType.Integer));
                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_type_str_{idx}", (object?)f.FieldTypeString ?? DBNull.Value, NpgsqlDbType.Varchar));
                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_sort_{idx}", f.OrderSort, NpgsqlDbType.Integer));
                            fieldParams.Add(new NpOnDbCommandParam<NpgsqlDbType>($"f_desc_{idx}", (object?)f.Description ?? DBNull.Value, NpgsqlDbType.Varchar));

                            itemModels.Add(new TblFldRModel
                            {
                                TblMaterId = tblId,
                                TblMaterCode = item.Code,
                                QueryDesc = item.Description,
                                ExecFunc = item.ExecFunc,
                                Query = item.Query,
                                FldMasterId = fldId,
                                FieldName = f.FieldName,
                                FieldType = f.FieldType.HasValue ? (NpgsqlDbType)f.FieldType.Value : null,
                                FieldTypeString = f.FieldTypeString,
                                ExecType = (EExecType)item.ExecType,
                                ServiceName = item.ServiceName,
                                DataBaseType = item.DbType.HasValue ? (EDb)item.DbType.Value : null,
                                OrderSort = f.OrderSort,
                            });
                        }

                        insertSql.Append(") AS v(id, field_name, field_type, field_type_string, order_sort, description) WHERE t.code = @code;");
                        txCommands.Add(new NpOnDbCommand(dbType, insertSql.ToString(), fieldParams));
                    }
                    else
                    {
                        itemModels.Add(new TblFldRModel
                        {
                            TblMaterId = tblId,
                            TblMaterCode = item.Code,
                            QueryDesc = item.Description,
                            ExecFunc = item.ExecFunc,
                            Query = item.Query,
                            ExecType = (EExecType)item.ExecType,
                            ServiceName = item.ServiceName,
                            DataBaseType = item.DbType.HasValue ? (EDb)item.DbType.Value : null,
                        });
                    }

                    syntheticFallback[item.Code] = itemModels;
                }

                // Single Postgres ACID transaction guarantees active readers never observe half-written parameters.
                var txResults = await postgresFactoryWrapper.ExecuteWithTransaction(txCommands);
                if (txResults == null || txResults.Count != txCommands.Count || txResults.Values.Any(v => !v.Status))
                {
                    response.SetFail("BulkSyncQueries transaction failed and rolled back cleanly.");
                    return;
                }

                // Reload newly committed view rows and swap L2 cache atomically via AddOrUpdate (NO Clear()).
                var committedGroups = new Dictionary<string, List<TblFldRModel>>(StringComparer.Ordinal);
                const string reloadSql = """
                    SELECT
                        vw.tbl_id       AS tbl_id,
                        vw.tbl_code     AS tbl_code,
                        vw.query_desc   AS query_desc,
                        vw.exec_func    AS exec_func,
                        vw.query        AS query,
                        vw.exec_type    AS exec_type,
                        vw.service_name AS service_name,
                        vw.db_type      AS db_type,
                        vw.fld_id       AS fld_id,
                        vw.order_sort   AS order_sort,
                        vw.field_name   AS field_name,
                        vw.field_type   AS field_type,
                        vw.field_type_string AS field_type_string
                    FROM vw_query_master vw
                    WHERE vw.tbl_code = ANY(string_to_array(@codes, ','))
                    ORDER BY vw.order_sort;
                    """;

                try
                {
                    var reloadResult = await postgresFactoryWrapper.ExecuteAsync(
                        reloadSql,
                        [new NpOnDbCommandParam<NpgsqlDbType>("codes", string.Join(',', distinctCodes), NpgsqlDbType.Text)]);

                    var loadedRows = reloadResult?
                        .GenericConverter(typeof(TblFldRModel))?
                        .Cast<TblFldRModel>()
                        .ToList();

                    if (loadedRows is { Count: > 0 })
                    {
                        foreach (var grp in loadedRows.Where(r => !string.IsNullOrEmpty(r.TblMaterCode)).GroupBy(r => r.TblMaterCode!))
                        {
                            committedGroups[grp.Key] = grp.ToList();
                        }
                    }
                }
                catch
                {
                    // Fallback to synthesized post-commit model list if view read is mocked or unavailable.
                }

                foreach (string code in distinctCodes)
                {
                    List<TblFldRModel> rows = committedGroups.TryGetValue(code, out var dbRows)
                        ? dbRows
                        : syntheticFallback[code];

                    internalCache.AddOrUpdate($"code:{code}", rows, QueryCacheTtl);
                    Guid? rowTblId = rows.FirstOrDefault()?.TblMaterId;
                    if (rowTblId.HasValue && rowTblId.Value != Guid.Empty)
                    {
                        internalCache.AddOrUpdate($"id:{rowTblId.Value}", rows, QueryCacheTtl);
                    }
                }

                // Publish single batch invalidation/sync event over Redis Pub/Sub.
                int broadcastReceivers = 0;
                if (redisBroadcastFactoryWrapper != null)
                {
                    try
                    {
                        var syncEvent = new RedisBroadcastMessage<GeneralQuerySyncBroadcastMessage>
                        {
                            Channel = GeneralQuerySyncBroadcastConstant.GeneralQuerySyncChannel,
                            Value = new GeneralQuerySyncBroadcastMessage
                            {
                                Codes = distinctCodes,
                                DeployVersion = deployVersion,
                                IsFullReload = command.IsFullReload,
                                PublishedAtUtc = DateTime.UtcNow,
                            }
                        };
                        var pubResult = await redisBroadcastFactoryWrapper.PublishAsync(syncEvent);
                        if (pubResult is RedisValueWrapper rw && rw.Status && rw.Parent.SingleValue.TryParse(out long count))
                        {
                            broadcastReceivers = (int)count;
                        }
                    }
                    catch
                    {
                        // DB + L2 write-through succeeded; Redis unavailability does not fail query deployment.
                    }
                }

                response.Data = new BulkSyncQueriesRModel
                {
                    SyncedCount = distinctCodes.Length,
                    DeployVersion = deployVersion,
                    BroadcastReceivers = broadcastReceivers,
                };
                response.SetSuccess();
            });
        }
    }
}
