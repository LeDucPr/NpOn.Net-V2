using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.NpOn.DbFactory.Generics;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null)
    {
        services.AddSingleton<INpOnPostgresFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            IDbFactoryWrapper factoryWrapper =
                new DbFactoryWrapper(connectionString, EDb.Postgres, (int)connectionNumber);
            return new NpOnPostgresFactoryWrapper(factoryWrapper);
        });
        return services;
    }
}