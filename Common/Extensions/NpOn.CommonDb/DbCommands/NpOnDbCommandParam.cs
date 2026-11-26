using System.Diagnostics.CodeAnalysis;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbCommandParam : INpOnDbCommandParam
{
    [SetsRequiredMembers]
    public NpOnDbCommandParam(string paramName, object? paramValue, object? paramType = null)
    {
        ParamName = paramName;
        ParamValue = paramValue;
        ParamType = paramType;
    }

    public NpOnDbCommandParam() { }

    public required string ParamName { get; set; }
    public object? ParamValue { get; set; }
    public virtual object? ParamType { get; set; }
}

public class NpOnDbCommandParam<TEnum> : NpOnDbCommandParam, INpOnDbCommandParam<TEnum> where TEnum : Enum
{
    private TEnum _paramType = default!;

    [SetsRequiredMembers]
    public NpOnDbCommandParam(string paramName, object? paramValue, TEnum paramType)
        : base(paramName, paramValue, paramType)
    {
        ParamType = paramType;
    }

    [SetsRequiredMembers]
    public NpOnDbCommandParam(string paramName, object? paramValue, object? paramType = null)
        : base(paramName, paramValue, paramType)
    {
        if (paramType is TEnum typedEnum)
            ParamType = typedEnum;
    }

    public NpOnDbCommandParam() { }

    public new required TEnum ParamType
    {
        get => _paramType;
        set
        {
            _paramType = value;
            base.ParamType = value;
        }
    }
}