using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.CassandraExtCm.Connections;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.MongoDbExtCm.Connections;
using Common.Infrastructures.NpOn.MssqlExtCm.Connections;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using Common.Infrastructures.NpOn.RedisExtCm.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.GenericDbFactory.FactoryResults;

public class GenericDbDriverFactory : BaseDbDriverFactory
{
    public GenericDbDriverFactory(EDb dbType, INpOnConnectOption option, int connectionNumber = 1)
        : base(dbType, option, connectionNumber)
    {
    }

    protected override NpOnDbConnection InitConnection()
    {
        if (Option == null)
        {
            throw new InvalidOperationException(
                "Connection options have not been set or are invalid. Call WithOptions() with valid options before creating connections.");
        }
        return DbType switch
        {
            EDb.Cassandra => CreateCassandraConnection(Option),
            EDb.ScyllaDb => CreateCassandraConnection(Option),
            EDb.Postgres => CreatePostgresConnection(Option),
            EDb.MongoDb => CreateMongoDbConnection(Option),
            EDb.Mssql => CreateMssqlDbConnection(Option),
            EDb.Redis => CreateRedisDbConnection(Option),
            _ => throw new NotSupportedException($"The database type '{DbType}' is not supported.")
        };
    }
    
    
    #region Cassandra

    private NpOnDbConnection CreateCassandraConnection(INpOnConnectOption option)
    {
        if (option is not CassandraConnectOption cassandraOptions)
        {
            throw new ArgumentException("Invalid options for Cassandra. Expected CassandraConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = CreateCassandraDriver(cassandraOptions);
        return new NpOnDbConnection<CassandraDriver>(driver);
    }

    private INpOnDbDriver CreateCassandraDriver(INpOnConnectOption option)
    {
        if (option is not CassandraConnectOption cassandraOptions)
        {
            throw new ArgumentException("Invalid options provided for CassandraCM. Expected CassandraConnectOptions.",
                nameof(option));
        }

        return new CassandraDriver(cassandraOptions);
    }

    #endregion Cassandra


    #region Postgres

    private NpOnDbConnection CreatePostgresConnection(INpOnConnectOption option)
    {
        if (option is not PostgresConnectOption postgresOptions)
        {
            throw new ArgumentException("Invalid options for Postgres. Expected PostgresConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = CreatePostgresDriver(postgresOptions);
        return new NpOnDbConnection<PostgresDriver>(driver);
    }

    private INpOnDbDriver CreatePostgresDriver(INpOnConnectOption option)
    {
        if (option is not PostgresConnectOption postgresOptions)
        {
            throw new ArgumentException("Invalid options provided for PostgresSQL. Expected PostgresConnectOptions.",
                nameof(option));
        }

        return new PostgresDriver(postgresOptions);
    }

    #endregion Postgres


    #region MongoDb

    private NpOnDbConnection CreateMongoDbConnection(INpOnConnectOption option)
    {
        if (option is not MongoDbConnectOption mongoOptions)
        {
            throw new ArgumentException("Invalid options for MongoDB. Expected MongoDbConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = CreateMongoDbDriver(mongoOptions);
        return new NpOnDbConnection<MongoDbDriver>(driver);
    }

    private INpOnDbDriver CreateMongoDbDriver(INpOnConnectOption option)
    {
        if (option is not MongoDbConnectOption mongoOptions)
        {
            throw new ArgumentException("Invalid options provided for MongoDB. Expected MongoDbConnectOptions.",
                nameof(option));
        }

        return new MongoDbDriver(mongoOptions);
    }

    #endregion MongoDb


    #region Mssql

    private NpOnDbConnection CreateMssqlDbConnection(INpOnConnectOption option)
    {
        if (option is not MssqlConnectOption mssqlOptions)
        {
            throw new ArgumentException("Invalid options for Mssql. Expected MssqlConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = CreateMssqlDriver(mssqlOptions);
        return new NpOnDbConnection<MssqlDriver>(driver);
    }

    private INpOnDbDriver CreateMssqlDriver(INpOnConnectOption option)
    {
        if (option is not MssqlConnectOption mssqlOptions)
        {
            throw new ArgumentException("Invalid options provided for Mssql. Expected MssqlConnectOptions.",
                nameof(option));
        }

        return new MssqlDriver(mssqlOptions);
    }

    #endregion Mssql


    #region Redis

    private NpOnDbConnection CreateRedisDbConnection(INpOnConnectOption option)
    {
        if (option is not RedisConnectOption redisOptions)
        {
            throw new ArgumentException("Invalid options for Redis. Expected RedisConnectOptions.",
                nameof(option));
        }

        INpOnDbDriver driver = CreateRedisDriver(redisOptions);
        return new NpOnDbConnection<RedisDriver>(driver);
    }

    private INpOnDbDriver CreateRedisDriver(INpOnConnectOption option)
    {
        if (option is not RedisConnectOption redisOptions)
        {
            throw new ArgumentException("Invalid options provided for Redis. Expected RedisConnectOptions.",
                nameof(option));
        }

        return new RedisDriver(redisOptions);
    }

    #endregion Redis
}