namespace Common.Infrastructures.NpOn.ICommonDb.DbCommands;

public interface INpOnDbExecFuncCommand : IBaseNpOnDbCommand
{
    string FuncName { get; }
}