using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbExecFuncCommand : INpOnDbExecFuncCommand
{
    private readonly EDb _eDb;
    private readonly string? _funcName;
    private readonly EDbLanguage? _dbLanguage;
    private readonly ILogger<NpOnDbCommand> _logger = new Logger<NpOnDbCommand>(new NullLoggerFactory());
    public List<INpOnDbCommandParam>? Parameters { get; }

    public NpOnDbExecFuncCommand(EDb eDb, string? funcName, IEnumerable<INpOnDbCommandParam>? parameters)
    {
        if (string.IsNullOrWhiteSpace(funcName))
            return; // funcName endpoint does not null or empty
        Parameters = parameters?.ToList();
        _funcName = funcName;
        try
        {
            _eDb = eDb;
            _dbLanguage = _eDb.ChooseLanguage();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex.Message);
        }
    }

    protected virtual bool CheckValid()
    {
        return true; // default 
    }

    // implements
    public bool IsValidCommandText => CheckValid();

    public string FuncName => _funcName ?? string.Empty;

    public EDb DataBaseType => _eDb;

    public EDbLanguage? DatabaseLanguage => _dbLanguage;
}