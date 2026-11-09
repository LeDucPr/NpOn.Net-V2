using System.Collections.Concurrent;
using System.Data;
using Common.Extensions.NpOn.CommonDb.Results;
using Common.Extensions.NpOn.ICommonDb.DbResults;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Common.Infrastructures.NpOn.PostgresExtCm.Results;
using NpgsqlTypes;
using Xunit;

namespace NpOn.Core.Tests;

public class PostgresDriverPerformanceTests
{
    [Fact]
    public void PostgresCell_StaticTypeCaching_ShouldProduceAccurateDbType()
    {
        // Act
        var cellInt = PostgresCell<int>.FromNpgsql(42, "int4", false);
        var cellGuid = PostgresCell<Guid>.FromNpgsql(Guid.NewGuid(), "uuid", true);
        var cellString = PostgresCell<string>.FromNpgsql("hello", "varchar", false);

        // Assert
        Assert.Equal(DbType.Int32, cellInt.DbType);
        Assert.Equal(42, cellInt.Value);
        Assert.False(cellInt.IsPrimaryKey);

        Assert.Equal(DbType.Guid, cellGuid.DbType);
        Assert.True(cellGuid.IsPrimaryKey);

        Assert.Equal(DbType.String, cellString.DbType);
        Assert.Equal("hello", cellString.Value);
    }

    [Fact]
    public void PostgresUtils_NormalizeForNpgsql_ShouldReturnAccurateNpgsqlDbType()
    {
        // Arrange
        var testId = Guid.NewGuid();
        var testDate = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var (valInt, typeInt) = PostgresUtils.NormalizeForNpgsql(100);
        var (valGuid, typeGuid) = PostgresUtils.NormalizeForNpgsql(testId);
        var (valDate, typeDate) = PostgresUtils.NormalizeForNpgsql(testDate);
        var (valStr, typeStr) = PostgresUtils.NormalizeForNpgsql("npon");

        // Assert
        Assert.Equal(NpgsqlDbType.Integer, typeInt);
        Assert.Equal(100, valInt);

        Assert.Equal(NpgsqlDbType.Uuid, typeGuid);
        Assert.Equal(testId, valGuid);

        Assert.Equal(NpgsqlDbType.TimestampTz, typeDate);
        Assert.Equal(testDate, valDate);

        Assert.Equal(NpgsqlDbType.Text, typeStr);
        Assert.Equal("npon", valStr);
    }

    [Fact]
    public void PostgresMappingExtensions_CreateRowMapper_ShouldCacheCompiledDelegate()
    {
        // Arrange
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["id"] = new("id", typeof(Guid), "uuid", true),
            ["username"] = new("username", typeof(string), "varchar", false),
            ["score"] = new("score", typeof(int), "int4", false)
        };
        var nameToIndexMap = new Dictionary<string, int>
        {
            ["id"] = 0,
            ["username"] = 1,
            ["score"] = 2
        };

        // Act: Generate twice
        var mapper1 = PostgresMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
        var mapper2 = PostgresMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);

        // Assert: Both instances must be the exact same cached delegate (zero re-compilation)
        Assert.Same(mapper1, mapper2);

        // Execute delegate to verify IL bytecode execution correctness
        var expectedId = Guid.NewGuid();
        var rowData = new object[] { expectedId, "kha_banh", 999 };
        var cells = mapper1(rowData);

        Assert.NotNull(cells);
        Assert.Equal(3, cells.Count);
        Assert.Equal(expectedId, cells["id"].ValueAsObject);
        Assert.Equal("kha_banh", cells["username"].ValueAsObject);
        Assert.Equal(999, cells["score"].ValueAsObject);
    }

    [Fact]
    public void PostgresRowWrapper_ReInit_ShouldRecycleStateWithoutAllocation()
    {
        // Arrange
        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["num"] = new("num", typeof(int), "int4", false)
        };
        var nameToIndexMap = new Dictionary<string, int> { ["num"] = 0 };
        var mapper = PostgresMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);

        var row1Data = new object[] { 111 };
        var rowWrapper = new PostgresRowWrapper(row1Data, mapper);

        // Act 1: First access
        var cells1 = rowWrapper.GetRowWrapper();
        Assert.Equal(111, cells1["num"].ValueAsObject);

        // Act 2: Recycle in-place with new data
        var row2Data = new object[] { 222 };
        rowWrapper.ReInit(row2Data, mapper);

        var cells2 = rowWrapper.GetRowWrapper();
        Assert.Equal(222, cells2["num"].ValueAsObject);
    }

    [Fact]
    public void Postgres_MultiThreaded_ConcurrentMapping_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 20;
        const int iterationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();

        var schemaMap = new Dictionary<string, NpOnColumnSchemaInfo>
        {
            ["id"] = new("id", typeof(Guid), "uuid", true),
            ["name"] = new("name", typeof(string), "varchar", false),
            ["count"] = new("count", typeof(int), "int4", false)
        };
        var nameToIndexMap = new Dictionary<string, int>
        {
            ["id"] = 0,
            ["name"] = 1,
            ["count"] = 2
        };

        // Act: Run concurrently from multiple threads
        Parallel.For(0, threadCount, threadIdx =>
        {
            try
            {
                var mapper = PostgresMappingExtensions.CreateRowMapper(schemaMap, nameToIndexMap);
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    var id = Guid.NewGuid();
                    var data = new object[] { id, $"user_{threadIdx}_{i}", i };
                    var cells = mapper(data);

                    if (!id.Equals(cells["id"].ValueAsObject) ||
                        !$"user_{threadIdx}_{i}".Equals(cells["name"].ValueAsObject) ||
                        !i.Equals(cells["count"].ValueAsObject))
                    {
                        throw new InvalidOperationException("Data corrupted during concurrent mapping");
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        Assert.Empty(exceptions);
    }

    private class MockFastTableWrapper : INpOnTableWrapper
    {
        public IReadOnlyDictionary<int, INpOnRowWrapper?> RowWrappers => new Dictionary<int, INpOnRowWrapper?>();
        public INpOnCollectionWrapper CollectionWrappers => null!;
        public IReadOnlyDictionary<string, int>? NameToIndexMap { get; set; }
        public IReadOnlyList<object[]>? RawData { get; set; }
        public IReadOnlyList<string>? PrimaryKeyColumns { get; set; }
    }

    [Fact]
    public void TablePostgresExtensions_FastPath_Insert_ShouldGenerateMultiRowSql()
    {
        // Arrange
        var table = new MockFastTableWrapper
        {
            NameToIndexMap = new Dictionary<string, int> { ["id"] = 0, ["name"] = 1 },
            RawData = new List<object[]>
            {
                new object[] { 1, "Alpha" },
                new object[] { 2, "Beta" }
            },
            PrimaryKeyColumns = new List<string> { "id" }
        };

        // Act
        var (sql, parameters) = table.ToPostgresParamsInsert("my_table");

        // Assert
        Assert.Contains("INSERT INTO my_table (\"id\",\"name\") VALUES (@p0,@p1), (@p2,@p3);", sql);
        Assert.Equal(4, parameters.Count);
        Assert.Equal(1, parameters[0].Value);
        Assert.Equal("Alpha", parameters[1].Value);
        Assert.Equal(2, parameters[2].Value);
        Assert.Equal("Beta", parameters[3].Value);
    }

    [Fact]
    public void TablePostgresExtensions_FastPath_Merge_ShouldGenerateUpsertSql()
    {
        // Arrange
        var table = new MockFastTableWrapper
        {
            NameToIndexMap = new Dictionary<string, int> { ["id"] = 0, ["name"] = 1 },
            RawData = new List<object[]>
            {
                new object[] { 1, "Alpha" },
                new object[] { 2, "Beta" }
            },
            PrimaryKeyColumns = new List<string> { "id" }
        };

        // Act
        var (sql, parameters) = table.ToPostgresParamsMerge("my_table");

        // Assert
        Assert.Contains("INSERT INTO my_table (\"id\",\"name\") VALUES (@p0,@p1), (@p2,@p3)", sql);
        Assert.Contains("ON CONFLICT (\"id\") DO UPDATE SET \"name\" = EXCLUDED.\"name\";", sql);
        Assert.Equal(4, parameters.Count);
    }

    [Fact]
    public void TablePostgresExtensions_FastPath_Delete_ShouldGenerateInClauseSql()
    {
        // Arrange
        var table = new MockFastTableWrapper
        {
            NameToIndexMap = new Dictionary<string, int> { ["id"] = 0, ["name"] = 1 },
            RawData = new List<object[]>
            {
                new object[] { 10, "Alpha" },
                new object[] { 20, "Beta" }
            },
            PrimaryKeyColumns = new List<string> { "id" }
        };

        // Act
        var (sql, parameters) = table.ToPostgresParamsDelete("my_table");

        // Assert
        Assert.Equal("DELETE FROM my_table WHERE \"id\" IN (@pk0, @pk1);", sql);
        Assert.Equal(2, parameters.Count);
        Assert.Equal(10, parameters[0].Value);
        Assert.Equal(20, parameters[1].Value);
    }
}

