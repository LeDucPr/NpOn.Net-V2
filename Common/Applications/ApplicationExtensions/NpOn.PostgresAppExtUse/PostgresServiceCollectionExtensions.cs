using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;
using Microsoft.Extensions.Configuration;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        var isUse = EApplicationConfiguration.IsUsePostgres.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;
        
        services.AddSingleton<IPostgresFactoryWrapper, PostgresFactoryWrapper>(sp =>
        {
            var config = sp.GetService<IConfiguration>();
            var pgConn = EApplicationConfiguration.PostgresConnectionString.GetAppSettingConfig();
            if (!string.IsNullOrWhiteSpace(pgConn))
            {
                connectionString = pgConn;
            }
            if (string.IsNullOrWhiteSpace(connectionString) && config != null)
            {
                connectionString = config["ConnectionString"] ?? config["PostgresConnectionString"];
            }

            int connNum = connectionNumber ?? EApplicationConfiguration.PostgresConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            if (connNum <= 0 && config != null)
            {
                int.TryParse(config["ConnectionNumber"] ?? config["PostgresConnectionNumber"], out connNum);
            }
            if (connNum <= 0) connNum = 100;

            PostgresFactoryWrapper factoryWrapper =
                new PostgresFactoryWrapper(connectionString ?? string.Empty, poolStore, connNum);
            return factoryWrapper;
        });
        return services;
    }
}