namespace Common.Extensions.NpOn.CommonDb.Results;

public class NpOnColumnSchemaInfo
{
    public string ColumnName { get; }
    public Type DataType { get; }
    public string ProviderDataTypeName { get; } // Ví dụ: "text", "int4"
    public bool IsPrimaryKey { get; }

    public NpOnColumnSchemaInfo(string columnName, Type dataType, string providerDataTypeName, bool isPrimaryKey = false)
    {
        ColumnName = columnName;
        DataType = dataType;
        ProviderDataTypeName = providerDataTypeName;
        IsPrimaryKey = isPrimaryKey;
    }
}