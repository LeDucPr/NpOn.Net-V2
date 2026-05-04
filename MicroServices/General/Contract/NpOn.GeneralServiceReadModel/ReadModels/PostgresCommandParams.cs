using Common.Extensions.NpOn.CommonDb.DbCommands;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;

[ProtoContract]
public class CommandRModel : BaseGeneralRModel
{
    [ProtoMember(1)] public required string CommandText { get; set; }
    [ProtoMember(2)] public byte[]? ParamsPayload { get; set; }
    [ProtoMember(3)] public required EExecType ExecType { get; set; }
    [ProtoMember(4)] public required EDb DatabaseType { get; set; }
    [ProtoMember(5)] public required Type DeserializeParamType { get; set; }

    private NpOnDbCommandParam[]? _parameters;

    public NpOnDbCommandParam[]? Parameters
    {
        get
        {
            if (_parameters == null && ParamsPayload != null)
            {
                _parameters = ((NpOnDbCommandParamGrpcList?)ProtoBufMode.ProtoBufDeserialize(ParamsPayload,
                        typeof(NpOnDbCommandParamGrpcList)))
                    ?.Items.Select(x => x.ToDbParam(DeserializeParamType)).ToArray();
            }

            return _parameters;
        }
        set => _parameters = value;
    }

    public NpOnDbExecuteCommand ToCommand()
    {
        return new NpOnDbExecuteCommand
        {
            CommandText = CommandText,
            ExecType = ExecType,
            Parameters = Parameters
        };
    }

    public virtual CommandRModel AddParameterValue(string paramName, object? paramValue, bool isCreateAsDefault = false)
    {
        var list = Parameters?.ToList() ?? new List<NpOnDbCommandParam>();
        var selectParam = list.FirstOrDefault(x => x.ParamName == paramName);
        if (selectParam != null)
        {
            selectParam.ParamValue = paramValue;
        }
        else if (isCreateAsDefault)
        {
            NpOnDbCommandParam newParam = DeserializeParamType is { IsEnum: true }
                ? (NpOnDbCommandParam)Activator.CreateInstance(
                    typeof(NpOnDbCommandParam<>).MakeGenericType(DeserializeParamType), paramName, paramValue, null)!
                : new NpOnDbCommandParam(paramName, paramValue);

            list.Add(newParam);
            Parameters = list.ToArray();
        }

        return this;
    }
}