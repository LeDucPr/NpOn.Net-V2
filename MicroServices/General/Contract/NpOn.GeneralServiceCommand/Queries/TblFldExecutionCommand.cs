using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;

[ProtoContract]
public class TblFldExecutionCommand : BaseGeneralCommand
{
    [ProtoMember(1)] public string? TblMaterId { get; set; }
    [ProtoMember(2)] public string? Code { get; set; }
    [ProtoMember(3)] public TblFldExecutionParamCommand[]? ExecParams { get; set; }
}

[ProtoContract]
public class TblFldExecutionParamCommand
{
    public TblFldExecutionParamCommand()
    {
    }

    public TblFldExecutionParamCommand(string paramName, string stringValue)
    {
        ParamName = paramName;
        StringValue = stringValue;
    }

    [ProtoMember(1)] public required string ParamName { get; set; }

    [ProtoMember(2)] public string? StringValue { get; set; }
    // [ProtoMember(3)] public Type? ParamType { get; set; } // if null => string 
}