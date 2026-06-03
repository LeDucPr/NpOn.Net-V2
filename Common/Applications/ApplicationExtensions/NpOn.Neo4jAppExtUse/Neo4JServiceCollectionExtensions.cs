using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;
using Common.Extensions.NpOn.CommonMode;
using Common.Infrastructures.DbFactories.NpOn.Neo4jDbFactory;

namespace Common.Applications.ApplicationsExtensions.NpOn.Neo4jAppExtUse;

public static class Neo4jServiceCollectionExtensions
{
    public static IServiceCollection AddNeo4j(this IServiceCollection services,
        string? connectionString = null, string? databaseName = null, int? connectionNumber = null)
    {
        var isUse = EApplicationConfiguration.IsUseNeo4j.GetAppSettingConfig().AsDefaultBool();
        if (!isUse) return services;
        
        services.AddSingleton<INeo4jFactoryWrapper, Neo4jFactoryWrapper>(sp =>
        {
            connectionString ??= EApplicationConfiguration.Neo4jConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??= EApplicationConfiguration.Neo4jConnectionNumber.GetAppSettingConfig().AsDefaultInt();
            databaseName ??= EApplicationConfiguration.Neo4jDatabaseName.GetAppSettingConfig().AsDefaultString();

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                databaseName = "neo4j";
            }

            Neo4jFactoryWrapper factoryWrapper = new Neo4jFactoryWrapper(
                openConnectString: connectionString, 
                databaseName: databaseName, 
                connectionNumber: (int)connectionNumber);
            
            return factoryWrapper;
        });

        return services;
    }
}
