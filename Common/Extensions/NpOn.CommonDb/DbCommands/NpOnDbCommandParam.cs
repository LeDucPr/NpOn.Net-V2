using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbCommandParam : INpOnDbCommandParam
{
    public required string ParamName { get; set; }
    public object? ParamValue { get; set; }
}

public class NpOnDbCommandParam<TEnum> : NpOnDbCommandParam, INpOnDbCommandParam<TEnum> where TEnum : Enum
{
    public required TEnum ParamType { get; set; }
}