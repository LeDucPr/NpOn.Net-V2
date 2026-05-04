using Common.Extensions.NpOn.CommonDb.DbCommands;
using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceReadModel.ReadModels;

[ProtoContract]
public class NpOnDbCommandParamGrpc
{
    [ProtoMember(1)] public required string ParamName { get; set; }
    [ProtoMember(2)] public string? ParamValue { get; set; }
    [ProtoMember(3)] public int? ParamType { get; set; }

    public NpOnDbCommandParam ToDbParam(Type? enumType = null)
    {
        if (enumType != null && enumType.IsEnum && ParamType.HasValue)
        {
            var enumValue = Enum.ToObject(enumType, ParamType.Value);
            var genericType = typeof(NpOnDbCommandParam<>).MakeGenericType(enumType);
            return (NpOnDbCommandParam)Activator.CreateInstance(genericType, ParamName, ParamValue, enumValue)!;
        }

        return new NpOnDbCommandParam(ParamName, ParamValue);
    }
}


public static class NpOnDbCommandParamGrpcExtensions
{
    public static NpOnDbCommandParam ToDbParam(this NpOnDbCommandParamGrpc gRpcParam, Type? enumType = null)
    {
        return gRpcParam.ToDbParam(enumType);
    }
}


[ProtoContract]
public class NpOnDbCommandParamGrpcList
{
    [ProtoMember(1)]
    public List<NpOnDbCommandParamGrpc> Items { get; set; } = new();
}
