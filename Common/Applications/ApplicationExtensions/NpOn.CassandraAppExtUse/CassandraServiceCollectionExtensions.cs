namespace NpOn.CassandraAppExtUse;

public static class CassandraServiceCollectionExtensions
{
    public static IServiceCollection AddPostgres(this IServiceCollection services,
        string? connectionString = null, int? connectionNumber = null, IObjectPoolStore? poolStore = null)
    {
        services.AddSingleton<IPostgresFactoryWrapper, PostgresFactoryWrapper>(sp =>
        {
            connectionString ??=
                EApplicationConfiguration.ConnectionString.GetAppSettingConfig().AsDefaultString();
            connectionNumber ??=
                EApplicationConfiguration.ConnectionNumber.GetAppSettingConfig().AsDefaultInt();

            PostgresFactoryWrapper factoryWrapper =
                new PostgresFactoryWrapper(connectionString, poolStore, (int)connectionNumber);
            return factoryWrapper;
        });
        return services;
    }
}