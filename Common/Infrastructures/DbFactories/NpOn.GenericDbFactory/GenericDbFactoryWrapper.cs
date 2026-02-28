using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Infrastructures.DbFactories.NpOn.BaseDbFactory.Generics;
using Common.Infrastructures.DbFactories.NpOn.GenericDbFactory.FactoryResults;
using Common.Infrastructures.NpOn.CommonDb.Connections;
using Common.Infrastructures.NpOn.ICommonDb.Connections;

namespace Common.Infrastructures.DbFactories.NpOn.GenericDbFactory;

public class GenericDbFactoryWrapper : BaseDbFactoryWrapper
{
    /// <summary>
    /// Tạo ra cho kết nối chỉ dùng ConnectionString hoặc lấy tham số khi khởi động
    /// </summary>
    /// <param name="openConnectString">Tham sô kết nối được mặc định cho khởi động là 1</param>
    /// <param name="dbType"></param>
    /// <param name="connectionNumber"></param>
    /// <param name="isUseCaching"></param>
    public GenericDbFactoryWrapper(
        string openConnectString, EDb dbType, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = dbType;
        var factoryCreator = new GenericDbDriverFactoryCreator(DbType, openConnectString, connectionNumber);
        Factory = factoryCreator.GetDbDriverFactory;
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
    public GenericDbFactoryWrapper(
        INpOnConnectOption connectOption, EDb dbType, int connectionNumber = 1, bool isUseCaching = true)
    {
        DbType = dbType;
        Factory = new GenericDbDriverFactory(dbType, connectOption, connectionNumber);
        if (isUseCaching)
            this.AddToDbFactoryWrapperCache();
    }
}