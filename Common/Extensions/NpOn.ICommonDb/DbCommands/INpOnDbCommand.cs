namespace Common.Extensions.NpOn.ICommonDb.DbCommands;

public interface INpOnDbCommand : IBaseNpOnDbCommand
{
    string CommandText { get; }
}