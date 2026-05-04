using ProtoBuf;

namespace MicroServices.General.Contract.NpOn.GeneralServiceCommand.Queries;

[ProtoContract]
public class TblFldExecutionCommand : BaseGeneralCommand
{
    public TblFldExecutionCommand()
    {
    }

    public TblFldExecutionCommand(string code)
    {
        Code = code;
    }

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

    public TblFldExecutionParamCommand(string paramName)
    {
        ParamName = paramName;
    }

    [ProtoMember(1)] public required string ParamName { get; set; }
}