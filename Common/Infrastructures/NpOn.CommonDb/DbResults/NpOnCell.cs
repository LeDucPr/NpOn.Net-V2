using System.Data;

namespace Common.Infrastructures.NpOn.CommonDb.DbResults;

/// <summary>
/// Primitive Data
/// </summary>
public interface INpOnCell
{
    object? ValueAsObject { get; } // Value of Cell
    Type ValueType { get; } // type of System .Net
    DbType DbType { get; } // type of ADO.Net 
    string SourceTypeName { get; }
}

public interface INpOnCell<out T> : INpOnCell
{
    T? Value { get; }
    void Create();
}

/// <summary>
/// Use for Primitive values
/// </summary>
/// <typeparam name="T"></typeparam>
public class NpOnCell<T> : INpOnCell<T>
{
    public T? Value { get; }
    public Type ValueType => typeof(T);
    public DbType DbType { get; }
    public string SourceTypeName { get; }
    public object? ValueAsObject => Value;

    public NpOnCell(object? value, DbType dbType, string sourceTypeName)
    {
        if (value == null || value == DBNull.Value)
        {
            Value = default;
        }
        else
        {
            Value = (T)value;
        }
        DbType = dbType;
        SourceTypeName = sourceTypeName;
    }

    public virtual void Create()
    {
        
    }
}