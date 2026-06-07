using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.Results;

public class ZeroMqCell : NpOnColumnSchemaInfo, INpOnCell
{
    public object? Value { get; set; }

    public T? GetValue<T>()
    {
        if (Value == null)
        {
            return default;
        }

        if (Value is T typedValue)
        {
            return typedValue;
        }

        try
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
        catch (InvalidCastException)
        {
            return default;
        }
        catch (FormatException)
        {
            return default;
        }
    }
}
