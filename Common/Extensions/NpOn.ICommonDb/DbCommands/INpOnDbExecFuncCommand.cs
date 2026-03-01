namespace Common.Extensions.NpOn.ICommonDb.DbCommands;

public interface INpOnDbExecFuncCommand : IBaseNpOnDbCommand
{
    string FuncName { get; }
}