using Common.Extensions.NpOn.CommonGrpcContract;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Commands;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using MySqlConnector;
using NpgsqlTypes;
using Riok.Mapperly.Abstractions;
using ProtoGeneral = NpOn.Protos.General.V1;

namespace MicroServices.General.Service.NpOn.GeneralService.Mappers;

/// <summary>
/// Compile-time zero-reflection mapper between GeneralService internal models and Schema-First .proto DTOs.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class FldMasterProtoMapper
{
    private static long ToUnixMs(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    public static TblFldExecutionCommand ToExecutionCommand(ProtoGeneral.TblFldExecutionCommand request)
    {
        var command = new TblFldExecutionCommand();
        if (request.HasTblMasterId)
            command.TblMaterId = request.TblMasterId;
        if (request.HasCode)
            command.Code = request.Code;
        if (request.ExecParams.Count > 0)
            command.ExecParams = request.ExecParams
                .Select(p => new TblFldExecutionParamCommand { ParamName = p.ParamName })
                .ToArray();
        return command;
    }

    public static BulkSyncQueriesCommand ToBulkSyncQueriesCommand(ProtoGeneral.BulkSyncQueriesCommand request)
    {
        var cmd = new BulkSyncQueriesCommand
        {
            DeployVersion = request.HasDeployVersion ? request.DeployVersion : null,
            IsFullReload = request.IsFullReload
        };

        foreach (var q in request.Queries)
        {
            var item = new TblMasterSyncItemCommand
            {
                Id = q.HasId ? q.Id : null,
                Code = q.Code,
                Description = q.HasDescription ? q.Description : null,
                ExecFunc = q.HasExecFunc ? q.ExecFunc : null,
                Query = q.HasQuery ? q.Query : null,
                ExecType = q.ExecType,
                ServiceName = q.HasServiceName ? q.ServiceName : null,
                DbType = q.HasDbType ? q.DbType : null,
            };

            foreach (var f in q.Fields)
            {
                item.Fields.Add(new FldQueryMasterSyncCommand
                {
                    Id = f.HasId ? f.Id : null,
                    FieldName = f.FieldName,
                    FieldType = f.HasFieldType ? f.FieldType : null,
                    FieldTypeString = f.HasFieldTypeString ? f.FieldTypeString : null,
                    OrderSort = f.OrderSort,
                    Description = f.HasDescription ? f.Description : null,
                });
            }

            cmd.Queries.Add(item);
        }

        return cmd;
    }

    public static ProtoGeneral.BulkSyncQueriesResponseProto ToBulkSyncQueriesResponse(
        CommonResponse<BulkSyncQueriesRModel?> source)
    {
        var reply = new ProtoGeneral.BulkSyncQueriesResponseProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };

        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data != null)
        {
            reply.SyncedCount = source.Data.SyncedCount;
            reply.DeployVersion = source.Data.DeployVersion;
            reply.BroadcastReceivers = source.Data.BroadcastReceivers;
        }

        return reply;
    }

    public static ProtoGeneral.CommandRModelResponseProto ToExecCommandReply(CommonResponse<CommandRModel?> source)
    {
        var reply = new ProtoGeneral.CommandRModelResponseProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };

        if (source.ErrorCode.HasValue)
            reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue)
            reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 })
            reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data != null)
            reply.Data = ToProtoCommandSpec(source.Data);

        return reply;
    }

    public static ProtoGeneral.CommandRModel ToProtoCommandSpec(CommandRModel source)
    {
        var spec = new ProtoGeneral.CommandRModel
        {
            CommandText = source.CommandText,
            ExecType = (int)source.ExecType,
            DatabaseType = (int)source.DatabaseType
        };

        if (source.Parameters is { Length: > 0 } parameters)
        {
            foreach (var p in parameters)
            {
                var paramSpec = new ProtoGeneral.NpOnDbCommandParamRModel
                {
                    ParamName = p.ParamName
                };
                if (p.ParamValue != null)
                    paramSpec.ParamValue = p.ParamValue.ToString();
                object? rawParamType = p switch
                {
                    INpOnDbCommandParam<NpgsqlDbType> pgParam => pgParam.ParamType,
                    INpOnDbCommandParam<MySqlDbType> mySqlParam => mySqlParam.ParamType,
                    INpOnDbCommandParam<System.Data.SqlDbType> sqlParam => sqlParam.ParamType,
                    _ => p.ParamType
                };
                if (rawParamType != null)
                    paramSpec.ParamType = Convert.ToInt32(rawParamType);
                spec.Parameters.Add(paramSpec);
            }
        }

        return spec;
    }

    public static ProtoGeneral.TblFldRModelListResponseProto ToExecutionReply(CommonResponse<List<TblFldRModel>> source)
    {
        var reply = new ProtoGeneral.TblFldRModelListResponseProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };

        if (source.ErrorCode.HasValue)
            reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue)
            reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 })
            reply.ErrorMessages.AddRange(source.ErrorMessages);

        if (source.Data is { Count: > 0 })
        {
            foreach (var item in source.Data)
            {
                reply.Items.Add(ToProtoTblFldItem(item));
            }
        }

        return reply;
    }

    [MapProperty(nameof(TblFldRModel.TblMaterId), nameof(ProtoGeneral.TblFldRModel.TblMasterId))]
    [MapProperty(nameof(TblFldRModel.TblMaterCode), nameof(ProtoGeneral.TblFldRModel.Code))]
    [MapProperty(nameof(TblFldRModel.DataBaseType), nameof(ProtoGeneral.TblFldRModel.DatabaseType))]
    public static partial ProtoGeneral.TblFldRModel ToProtoTblFldItem(TblFldRModel source);

    private static string? MapNullableGuid(Guid? value) => value?.ToString();

    private static int? MapNullableEnum<TEnum>(TEnum? value) where TEnum : struct, Enum
        => value.HasValue ? Convert.ToInt32(value.Value) : null;
}
