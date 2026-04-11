using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.ClickHouseFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.ClickHouseAppExtUse;

public static class ClickHouseServiceCollectionExtensions
{
    public static IServiceCollection AddClickHouse(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        var isUse = EApplicationConfiguration.IsUseClickhouse.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;

        services.AddSingleton<IClickHouseFactoryWrapper, ClickHouseFactoryWrapper>(_ =>
        {
            connectionString ??= EApplicationConfiguration.ClickhouseConnectionString.GetAppSettingConfig()
                .AsDefaultString();
            connectionNumber ??= EApplicationConfiguration.ClickhouseConnectionNumber.GetAppSettingConfig()
                .AsDefaultInt();

            return new ClickHouseFactoryWrapper(connectionString, poolStore, (int)connectionNumber);
        });

        return services;
    }
}