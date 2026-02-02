

using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Infrastructures.NpOn.CommonDb.DbCommands;

public class NpOnDbExecuteCommand
{
    public required string CommandText { get; set; }
    public required EExecType ExecType { get; set; }
    public NpOnDbCommandParam[]? Parameters { get; set; }
}