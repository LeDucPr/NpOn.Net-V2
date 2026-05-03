using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.MySqlFactory;

namespace Common.Applications.ApplicationsExtensions.NpOn.MySqlAppExtUse;

public static class MySqlServiceCollectionExtensions
{
    public static IServiceCollection AddMySql(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        services.AddSingleton<IMySqlFactoryWrapper, MySqlFactoryWrapper>(sp =>
        {
            connectionString ??=
                EApplicationConfiguration.MySqlConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.MySqlConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            MySqlFactoryWrapper factoryWrapper =
                new MySqlFactoryWrapper(connectionString, poolStore, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}