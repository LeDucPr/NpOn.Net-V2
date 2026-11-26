using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;
using MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;
using NpOn.Protos.General.V1;
using Riok.Mapperly.Abstractions;

namespace MicroServices.General.Service.NpOn.GeneralService.Mappers;

/// <summary>
/// Compile-time zero-reflection mapper between GeneralService internal models and Schema-First .proto DTOs.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class FldMasterProtoMapper
{
    public static TblFldExecutionCommand ToExecutionCommand(GetExecCommandRequest request)
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

    public static GetExecCommandReply ToExecCommandReply(CommonResponse<CommandRModel?> source)
    {
        var reply = new GetExecCommandReply
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = new DateTimeOffset(DateTime.SpecifyKind(source.ServerTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
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

    public static CommandSpec ToProtoCommandSpec(CommandRModel source)
    {
        var spec = new CommandSpec
        {
            CommandText = source.CommandText,
            ExecType = (int)source.ExecType,
            DatabaseType = (int)source.DatabaseType
        };

        if (source.Parameters is { Length: > 0 } parameters)
        {
            foreach (var p in parameters)
            {
                var paramSpec = new CommandParamSpec
                {
                    ParamName = p.ParamName
                };
                if (p.ParamValue != null)
                    paramSpec.ParamValue = p.ParamValue.ToString();
                if (p.ParamType != null)
                    paramSpec.ParamType = Convert.ToInt32(p.ParamType);
                spec.Parameters.Add(paramSpec);
            }
        }

        return spec;
    }

    public static GetExecutionReply ToExecutionReply(CommonResponse<List<TblFldRModel>> source)
    {
        var reply = new GetExecutionReply
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = new DateTimeOffset(DateTime.SpecifyKind(source.ServerTime, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
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

    [MapProperty(nameof(TblFldRModel.TblMaterId), nameof(TblFldItem.TblMasterId))]
    [MapProperty(nameof(TblFldRModel.TblMaterCode), nameof(TblFldItem.Code))]
    [MapProperty(nameof(TblFldRModel.DataBaseType), nameof(TblFldItem.DatabaseType))]
    public static partial TblFldItem ToProtoTblFldItem(TblFldRModel source);

    private static string? MapNullableGuid(Guid? value) => value?.ToString();

    private static int? MapNullableEnum<TEnum>(TEnum? value) where TEnum : struct, Enum
        => value.HasValue ? Convert.ToInt32(value.Value) : null;
}
