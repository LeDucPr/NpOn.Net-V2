using Common.Infrastructures.NpOn.BaseRepository;
using Common.Infrastructures.NpOn.CommonDb.DbResults;
using Common.Infrastructures.NpOn.DbFactory.Generics;

namespace Common.Applications.ApplicationsExtensions.NpOn.PostgresAppExtUse;

public interface INpOnPostgresFactoryWrapper : IDbFactoryWrapper
{
    Task<INpOnWrapperResult?> Execute(NpOnRepositoryCommand npOnRepositoryCommand);
}