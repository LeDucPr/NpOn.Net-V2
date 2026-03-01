using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbExecuteCommand
{
    public required string CommandText { get; set; }
    public required EExecType ExecType { get; set; }
    public INpOnDbCommandParam[]? Parameters { get; set; }
}