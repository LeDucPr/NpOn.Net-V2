using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.ICommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.CommonDb.DbCommands;

public class NpOnDbExecuteCommand
{
    public required string CommandText { get; set; }
    public required EExecType ExecType { get; set; }
    public INpOnDbCommandParam[]? Parameters { get; set; }
}