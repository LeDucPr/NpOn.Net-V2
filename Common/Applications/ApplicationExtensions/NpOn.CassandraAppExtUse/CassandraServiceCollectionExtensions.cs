using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.CassandraFactory;

namespace NpOn.CassandraAppExtUse;

public static class CassandraServiceCollectionExtensions
{
    public static IServiceCollection AddCassandra(this IServiceCollection services,
        string keyspace, string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        services.AddSingleton<ICassandraFactoryWrapper, CassandraFactoryWrapper>(sp =>
        {
            connectionString ??=
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            var contactAddresses = string.IsNullOrWhiteSpace(connectionString) 
                ? Array.Empty<string>() 
                : connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries);

            CassandraFactoryWrapper factoryWrapper =
                new CassandraFactoryWrapper(keyspace, contactAddresses, poolStore, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}