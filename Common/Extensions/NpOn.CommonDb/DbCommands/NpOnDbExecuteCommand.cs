using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbExecuteCommand
{
    public required string CommandText { get; set; }
    public required EExecType ExecType { get; set; }
    public INpOnDbCommandParam[]? Parameters { get; set; }

    public virtual NpOnDbExecuteCommand AddParameterValue(string paramName, object? paramValue)
    {
        var list = Parameters?.ToList() ?? new List<INpOnDbCommandParam>();
        INpOnDbCommandParam? selectParam = list.FirstOrDefault(x => x.ParamName == paramName);
        if (selectParam != null)
        {
            selectParam.ParamValue = paramValue;
        }
        else
        {
            list.Add(new NpOnDbCommandParam(paramName, paramValue));
            Parameters = list.ToArray();
        }

        return this;
    }
}

public class NpOnDbExecuteCommand<TEnum> : NpOnDbExecuteCommand where TEnum : Enum
{
    public NpOnDbExecuteCommand<TEnum> AddParameterValue(string paramName, object? paramValue, TEnum? paramType = default)
    {
        var list = Parameters?.ToList() ?? new List<INpOnDbCommandParam>();
        INpOnDbCommandParam? selectParam = list.FirstOrDefault(x => x.ParamName == paramName);
        
        if (selectParam is INpOnDbCommandParam<TEnum> typedParam)
        {
            typedParam.ParamValue = paramValue;
            if (paramType != null)
                typedParam.ParamType = paramType;
        }
        else if (selectParam != null)
        {
            selectParam.ParamValue = paramValue;
        }
        else
        {
            list.Add(new NpOnDbCommandParam<TEnum>(paramName, paramValue, paramType ?? default!));
            Parameters = list.ToArray();
        }

        return this;
    }

    public override NpOnDbExecuteCommand<TEnum> AddParameterValue(string paramName, object? paramValue)
    {
        return AddParameterValue(paramName, paramValue, default);
    }
}