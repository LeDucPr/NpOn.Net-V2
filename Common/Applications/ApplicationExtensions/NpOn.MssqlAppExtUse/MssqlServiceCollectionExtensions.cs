using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.MssqlFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.MssqlAppExtUse;

public static class MssqlServiceCollectionExtensions
{
    public static IServiceCollection AddMssql(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        var isUse = EApplicationConfiguration.IsUseMssql.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;

        services.AddSingleton<IMssqlFactoryWrapper, MssqlFactoryWrapper>(sp =>
        {
            connectionString ??=
                EApplicationConfiguration.MssqlConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.MssqlConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            MssqlFactoryWrapper factoryWrapper =
                new MssqlFactoryWrapper(connectionString, poolStore, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}
