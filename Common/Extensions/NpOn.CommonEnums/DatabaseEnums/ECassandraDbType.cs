namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

public enum ECassandraDbType
{
    Unknown = 40,
    Bigint = 1,
    Boolean = 2,
    Blob = 4,      // Maps to Bytea
    Ascii = 6,     // Maps to Char
    Date = 7,
    Double = 8,
    Int = 9,       // Maps to Integer
    Decimal = 13,  // Maps to Numeric
    Float = 17,    // Maps to Real
    SmallInt = 18,
    Text = 19,
    Time = 20,
    Timestamp = 21,
    Varchar = 22,
    Inet = 24,
    Uuid = 27,
    Duration = 30, // Maps to Interval
    Counter = 100, // Custom (no PG equivalent for Counter)
    Varint = 101,  // Custom (no direct PG equivalent)
    Timeuuid = 102,// Custom (no direct PG equivalent)
    TinyInt = 103, // Custom (PG doesn't have tinyint natively mapped this way)
    Udt = 104,
    Tuple = 105,
    List = -2147483648, // Array flag
    Set = 1073741824,   // Range flag / Set flag
    Map = 536870912     // Multirange flag / Map flag
}
