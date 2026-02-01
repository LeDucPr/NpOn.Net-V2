using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.DbFactory.Generics;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.PostgresExtCm.Connections;
using NpOn.PostgresDbFactory.FactoryResults;

namespace NpOn.PostgresDbFactory;

public class PostgresDbFactoryWrapper : BaseDbFactoryWrapper
{
    /// <summary>
    /// Tạo ra cho kết nối chỉ dùng ConnectionString hoặc lấy tham số khi khởi động
    /// </summary>
    /// <param name="openConnectString">Tham sô kết nối được mặc định cho khởi động là 1</param>
    /// <param name="dbType"></param>
    /// <param name="connectionNumber"></param>
    /// <param name="isUseCaching"></param>
    public PostgresDbFactoryWrapper(
        string openConnectString, EDb dbType, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = dbType;
        Factory = new PostgresDbDriverFactory(
            new PostgresConnectOption()
                .SetConnectionString(openConnectString),
            connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }

    /// <summary>
    /// Generic initial
    /// </summary>
    /// <param name="connectOption"></param>
    /// <param name="dbType"></param>
    /// <param name="connectionNumber"></param>
    /// <param name="isUseCaching"></param>
    public PostgresDbFactoryWrapper(
        INpOnConnectOption connectOption, EDb dbType, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = dbType;
        if (connectOption is not PostgresConnectOption)
            throw new ArgumentException("connectOption must be a PostgresConnectOption");
        Factory = new PostgresDbDriverFactory(connectOption, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }
}