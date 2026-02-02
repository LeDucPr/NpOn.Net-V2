using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.PostgresDbFactory;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public static class PostgresServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null)
    {
        services.AddSingleton<IPostgresFactoryWrapper, PostgresFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            PostgresFactoryWrapper factoryWrapper =
                new PostgresFactoryWrapper(connectionString, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}