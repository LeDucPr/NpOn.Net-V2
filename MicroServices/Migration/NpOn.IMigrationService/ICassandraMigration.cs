using Common.Extensions.NpOn.CommonGrpcContract;

namespace MicroServices.Migration.Service.NpOn.IMigrationService;

public interface ICassandraMigration
{
    Task<CommonResponse> TransferTable();
}