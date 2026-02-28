using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.CommonDb.DbCommands;

public class NpOnDbCommandParam : INpOnDbCommandParam
{
    public required string ParamName { get; set; }
    public object? ParamValue { get; set; }
}

public class NpOnDbCommandParam<TEnum> : NpOnDbCommandParam, INpOnDbCommandParam<TEnum> where TEnum : Enum
{
    public required TEnum ParamType { get; set; }
}