namespace Common.Infrastructures.NpOn.ICommonDb.DbCommands;

public interface INpOnDbCommand : IBaseNpOnDbCommand
{
    string CommandText { get; }
}