using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.ICommonDb.DbCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Common.Extensions.NpOn.CommonDb.DbCommands;

public class NpOnDbCommand : INpOnDbCommand
{
    private readonly EDb _eDb;
    private readonly EDbLanguage? _dbLanguage;
    private readonly ILogger<NpOnDbCommand> _logger = new Logger<NpOnDbCommand>(new NullLoggerFactory());
    private List<INpOnDbCommandParam>? _parameters;

    public NpOnDbCommand(EDb eDb, string? commandText)
    {
        CommandText = commandText ?? string.Empty;
        try
        {
            _eDb = eDb;
            _dbLanguage = _eDb.ChooseLanguage();
            CommandText = commandText;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex.Message);
        }
    }


    public NpOnDbCommand(EDb eDb, string? commandText, List<INpOnDbCommandParam>? parameters)
    {
        CommandText = commandText ?? string.Empty;
        try
        {
            _eDb = eDb;
            _dbLanguage = _eDb.ChooseLanguage();
            CommandText = commandText;
            _parameters = parameters;
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

    public string CommandText => field ?? string.Empty;

    public EDb DataBaseType => _eDb;

    public EDbLanguage? DatabaseLanguage => _dbLanguage;

    public List<INpOnDbCommandParam>? Parameters => _parameters;
}