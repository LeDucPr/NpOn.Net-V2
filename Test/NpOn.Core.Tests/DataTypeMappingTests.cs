using System.Text.Json;
using Common.Extensions.NpOn.ICommonDb.DbResults.Extensions;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using NpgsqlTypes;
using Xunit;

namespace NpOn.Core.Tests;

public enum ETestRole
{
    Guest = 0,
    User = 1,
    Admin = 2
}

public class UserProfileDto
{
    public string Nickname { get; set; } = string.Empty;
    public int Level { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class AllTypesModel
{
    public int Id { get; set; }
    public long BigNumber { get; set; }
    public short SmallNumber { get; set; }
    public byte ByteValue { get; set; }
    public double DoubleValue { get; set; }
    public float FloatValue { get; set; }
    public decimal DecimalValue { get; set; }
    public bool IsActive { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid UniqueCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public TimeSpan Duration { get; set; }
    public ETestRole Role { get; set; }
    public int? NullableInt { get; set; }
    public Guid? NullableGuid { get; set; }
    public string[] StringArray { get; set; } = [];
    public int[] IntArray { get; set; } = [];
    public List<string> StringList { get; set; } = new();
    public UserProfileDto Profile { get; set; } = new();
}

public class DataTypeMappingTests
{
    [Fact]
    public void NpOnTypeConverter_ShouldConvertAllPrimitiveAndValueTypes()
    {
        Assert.Equal(42, NpOnTypeConverter.ConvertDbValue(42L, typeof(int)));
        Assert.Equal(100L, NpOnTypeConverter.ConvertDbValue(100, typeof(long)));
        Assert.Equal((short)5, NpOnTypeConverter.ConvertDbValue(5, typeof(short)));
        Assert.Equal((byte)255, NpOnTypeConverter.ConvertDbValue(255, typeof(byte)));
        Assert.Equal(123.5, NpOnTypeConverter.ConvertDbValue(123.5f, typeof(double)));
        Assert.Equal(99.99m, NpOnTypeConverter.ConvertDbValue(99.99, typeof(decimal)));
        Assert.True((bool)NpOnTypeConverter.ConvertDbValue(1, typeof(bool))!);
        Assert.False((bool)NpOnTypeConverter.ConvertDbValue("false", typeof(bool))!);

        var guid = Guid.NewGuid();
        Assert.Equal(guid, NpOnTypeConverter.ConvertDbValue(guid.ToString(), typeof(Guid)));
        Assert.Equal(guid, NpOnTypeConverter.ConvertDbValue(guid.ToByteArray(), typeof(Guid)));

        var now = DateTime.UtcNow;
        Assert.Equal(now, NpOnTypeConverter.ConvertDbValue(now, typeof(DateTime)));

        var date = new DateOnly(2026, 11, 18);
        Assert.Equal(date, NpOnTypeConverter.ConvertDbValue(new DateTime(2026, 11, 18), typeof(DateOnly)));
        Assert.Equal(date, NpOnTypeConverter.ConvertDbValue("2026-11-18", typeof(DateOnly)));

        var time = new TimeOnly(14, 30, 0);
        Assert.Equal(time, NpOnTypeConverter.ConvertDbValue(new TimeSpan(14, 30, 0), typeof(TimeOnly)));
        Assert.Equal(time, NpOnTypeConverter.ConvertDbValue("14:30:00", typeof(TimeOnly)));
    }

    [Fact]
    public void NpOnTypeConverter_ShouldConvertEnumsAndNullables()
    {
        Assert.Equal(ETestRole.Admin, NpOnTypeConverter.ConvertDbValue(2, typeof(ETestRole)));
        Assert.Equal(ETestRole.Admin, NpOnTypeConverter.ConvertDbValue("Admin", typeof(ETestRole)));
        Assert.Equal(ETestRole.User, NpOnTypeConverter.ConvertDbValue(1L, typeof(ETestRole)));

        Assert.Equal(123, NpOnTypeConverter.ConvertDbValue(123, typeof(int?)));
        Assert.Null(NpOnTypeConverter.ConvertDbValue(DBNull.Value, typeof(int?)));
        Assert.Null(NpOnTypeConverter.ConvertDbValue(null, typeof(Guid?)));

        var guid = Guid.NewGuid();
        Assert.Equal(guid, NpOnTypeConverter.ConvertDbValue(guid, typeof(Guid?)));
    }

    [Fact]
    public void NpOnTypeConverter_ShouldConvertArraysAndCollections()
    {
        // Native array to T[]
        object rawIntArray = new int[] { 1, 2, 3 };
        var convertedIntArray = (int[])NpOnTypeConverter.ConvertDbValue(rawIntArray, typeof(int[]))!;
        Assert.Equal([1, 2, 3], convertedIntArray);

        // Native array to List<T>
        object rawStringArray = new string[] { "apple", "banana", "cherry" };
        var convertedList = (List<string>)NpOnTypeConverter.ConvertDbValue(rawStringArray, typeof(List<string>))!;
        Assert.Equal(["apple", "banana", "cherry"], convertedList);

        // JSON string to array
        string jsonArr = "[\"dev\",\"qa\",\"ops\"]";
        var fromJsonArray = (string[])NpOnTypeConverter.ConvertDbValue(jsonArr, typeof(string[]))!;
        Assert.Equal(["dev", "qa", "ops"], fromJsonArray);

        // JSON string to List<string>
        var fromJsonList = (List<string>)NpOnTypeConverter.ConvertDbValue(jsonArr, typeof(List<string>))!;
        Assert.Equal(["dev", "qa", "ops"], fromJsonList);
    }

    [Fact]
    public void NpOnTypeConverter_ShouldConvertJsonbToComplexObject()
    {
        var profile = new UserProfileDto
        {
            Nickname = "HackerOne",
            Level = 99,
            Tags = ["vip", "legend"]
        };
        string json = JsonSerializer.Serialize(profile);

        var converted = (UserProfileDto)NpOnTypeConverter.ConvertDbValue(json, typeof(UserProfileDto))!;
        Assert.NotNull(converted);
        Assert.Equal("HackerOne", converted.Nickname);
        Assert.Equal(99, converted.Level);
        Assert.Equal(["vip", "legend"], converted.Tags);
    }

    [Fact]
    public void PostgresUtils_NormalizeForNpgsql_ShouldInferCorrectPostgresTypes()
    {
        // Array
        var (valArr, dbTypeArr) = PostgresUtils.NormalizeForNpgsql(new string[] { "a", "b" });
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Text, dbTypeArr);

        var (valIntArr, dbTypeIntArr) = PostgresUtils.NormalizeForNpgsql(new int[] { 10, 20 });
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Integer, dbTypeIntArr);

        // Complex Object to Jsonb
        var profile = new UserProfileDto { Nickname = "Test", Level = 1 };
        var (valObj, dbTypeObj) = PostgresUtils.NormalizeForNpgsql(profile);
        Assert.Equal(NpgsqlDbType.Jsonb, dbTypeObj);
        Assert.Contains("\"Nickname\":\"Test\"", (string)valObj!);

        // Enum
        var (valEnum, dbTypeEnum) = PostgresUtils.NormalizeForNpgsql(ETestRole.Admin);
        Assert.Equal(2, valEnum);
        Assert.Equal(NpgsqlDbType.Integer, dbTypeEnum);

        // DateOnly / TimeOnly
        var (valDate, dbTypeDate) = PostgresUtils.NormalizeForNpgsql(new DateOnly(2026, 11, 18));
        Assert.Equal(NpgsqlDbType.Date, dbTypeDate);

        var (valTime, dbTypeTime) = PostgresUtils.NormalizeForNpgsql(new TimeOnly(12, 0));
        Assert.Equal(NpgsqlDbType.Time, dbTypeTime);
    }
}

