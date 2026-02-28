namespace Common.Infrastructures.NpOn.ICommonDb.DbCommands;

public interface INpOnDbCommandParam
{
    public string ParamName { get; set; }
    public object? ParamValue { get; set; }
}

public interface INpOnDbCommandParam<TEnum> : INpOnDbCommandParam where TEnum : Enum
{
    public TEnum ParamType { get; set; }
}