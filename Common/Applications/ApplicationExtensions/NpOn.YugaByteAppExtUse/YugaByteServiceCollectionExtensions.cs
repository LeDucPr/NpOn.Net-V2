using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonInternalCache.ObjectPoolings;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.YugaByteFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.YugaByteAppExtUse;

public static class YugaByteServiceCollectionExtensions
{
    public static IServiceCollection AddYugaBytePg(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        var isUse = EApplicationConfiguration.IsUseYugaBytePg.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;

        services.AddSingleton<IYugaByteFactoryWrapper, YugaByteFactoryWrapper>(sp =>
        {
            connectionString ??=
                EApplicationConfiguration.YugaBytePgConnectStrings.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.YugaBytePgConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            var factoryWrapper = new YugaByteFactoryWrapper(connectionString, poolStore, (int)connectionNumber);
            return (YugaByteFactoryWrapper)factoryWrapper;
        });
        return services;
    }
}
