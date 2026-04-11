using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.ElasticSearchFactory;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Applications.ApplicationsExtensions.NpOn.ElasticSearchAppExtUse;

public static class ElasticSearchServiceCollectionExtensions
{
    public static IServiceCollection AddElasticSearch(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null)
    {
        var isUse = EApplicationConfiguration.IsUseElasticSearch.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;

        services.AddSingleton<IElasticSearchFactoryWrapper, ElasticSearchFactoryWrapper>(_ =>
        {
            connectionString ??=
                EApplicationConfiguration.ElasticSearchConnectStrings.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??= EApplicationConfiguration.ElasticSearchConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            
            IElasticSearchFactoryWrapper factoryWrapper =
                new ElasticSearchFactoryWrapper(connectionString, (int)connectionNumber, true);
            return (ElasticSearchFactoryWrapper)factoryWrapper;
        });
        return services;
    }
}
