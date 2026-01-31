using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.NpOn.CommonDb.DbCommands;

namespace Common.Infrastructures.NpOn.BaseExecution;

public class NpOnExecuteCommand
{
    public required string CommandText { get; set; }
    public required EExecType ExecType { get; set; }
    public NpOnDbCommandParam[]? Parameters { get; set; }
}