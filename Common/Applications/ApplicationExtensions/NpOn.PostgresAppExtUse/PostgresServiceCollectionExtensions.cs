using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.DbFactory.Generics;
using NpOn.PostgresDbFactory;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null)
    {
        services.AddSingleton<PostgresDbFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            PostgresDbFactoryWrapper factoryWrapper =
                new PostgresDbFactoryWrapper(connectionString, EDb.Postgres, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}