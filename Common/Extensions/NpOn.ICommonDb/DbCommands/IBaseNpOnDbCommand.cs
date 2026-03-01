using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Extensions.NpOn.ICommonDb.DbCommands;

public interface IBaseNpOnDbCommand
{
    // for output
    bool IsValidCommandText { get; }
    EDb DataBaseType { get; }
    EDbLanguage? DatabaseLanguage { get; }
    List<INpOnDbCommandParam>? Parameters { get; }
}