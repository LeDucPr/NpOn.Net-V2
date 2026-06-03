using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

public enum EDb : byte
{
    [Display(Name = "Unknown")] Unknown = 0,
    [Display(Name = "Cassandra")] Cassandra = 1,
    [Display(Name = "ScyllaDb")] ScyllaDb = 2,
    [Display(Name = "Postgres")] Postgres = 3,
    [Display(Name = "MongoDb")] MongoDb = 4,
    [Display(Name = "Mssql")] Mssql = 5,
    [Display(Name = "Redis")] Redis = 6,
    [Display(Name = "ElasticSearch")] ElasticSearch = 7,
    [Display(Name = "YugaBytePg")] YugaBytePg = 8,
    [Display(Name = "ClickHouse")] ClickHouse = 9,
    [Display(Name = "MySql")] MySql = 10,
    [Display(Name = "Neo4j")] Neo4j = 11,
}

public static class EDbExtension
{
    public static EDbLanguage ChooseLanguage(this EDb db)
    {
        return db switch
        {
            EDb.Unknown => throw new NotSupportedException($"The database language for '{db}' is not supported."),
            EDb.Cassandra => EDbLanguage.Cql,
            EDb.ScyllaDb => EDbLanguage.Cql,
            EDb.Postgres => EDbLanguage.Sql,
            EDb.Mssql => EDbLanguage.Sql,
            EDb.MongoDb => EDbLanguage.Bson,
            EDb.Redis => EDbLanguage.Json,
            EDb.ElasticSearch => EDbLanguage.Json,
            EDb.YugaBytePg => EDbLanguage.Sql,
            EDb.ClickHouse => EDbLanguage.Sql,
            EDb.Neo4j => EDbLanguage.Cypher,
            _ => throw new NotSupportedException($"The database language for '{db}' is not supported."),
        };
    }

    public static bool IsValid(this EDb dbType)
    {
        List<EDb> validTypes =
        [
            EDb.Cassandra,
            EDb.ScyllaDb,
            EDb.Postgres,
            EDb.Mssql,
            EDb.MongoDb,
            EDb.Redis,
            EDb.ElasticSearch,
            EDb.YugaBytePg,
            EDb.ClickHouse,
            EDb.Neo4j,
        ];
        if (validTypes.Contains(dbType)) return true;
        return false;
    }
}