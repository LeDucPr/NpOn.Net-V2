using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.CassandraFactory;

namespace Common.Applications.ApplicationsExtensions.NpOn.CassandraAppExtUse;

public static class CassandraServiceCollectionExtensions
{
    public static IServiceCollection AddCassandra(this IServiceCollection services,
        string? keyspace = null, string? connectionString = null, int? connectionNumber = null,
        IObjectPoolStore? poolStore = null)
    {
        var isUse = EApplicationConfiguration.IsUseCassandra.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;
        
        services.AddSingleton<ICassandraFactoryWrapper, CassandraFactoryWrapper>(sp =>
        {
            keyspace ??= EApplicationConfiguration.CassandraKeySpace.GetAppSettingConfig().AsDefaultString();
            connectionString ??= EApplicationConfiguration.CassandraConnectionString.GetAppSettingConfig()
                .AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.CassandraConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            var contactAddresses = string.IsNullOrWhiteSpace(connectionString)
                ? Array.Empty<string>()
                : connectionString.Split(',', StringSplitOptions.RemoveEmptyEntries);

            CassandraFactoryWrapper factoryWrapper =
                new CassandraFactoryWrapper(connectionString, keyspace, contactAddresses, poolStore,
                    (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}