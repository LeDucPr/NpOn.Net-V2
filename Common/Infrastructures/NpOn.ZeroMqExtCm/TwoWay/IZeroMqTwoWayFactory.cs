using Common.Extensions.NpOn.CommonEnums.AppConfigEnums;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public interface IZeroMqTwoWayFactory
{
    Task<IZeroMqTwoWayProvider> CreateClientAsync(EUrlConfiguration urlConfig);
}