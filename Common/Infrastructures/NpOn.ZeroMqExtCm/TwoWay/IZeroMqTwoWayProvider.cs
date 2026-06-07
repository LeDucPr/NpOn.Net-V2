using System;
using System.Threading.Tasks;
using Common.Extensions.NpOn.ICommonDb.DbResults;

namespace Common.Infrastructures.NpOn.ZeroMqExtCm.TwoWay;

public interface IZeroMqTwoWayProvider : IDisposable
{
    int HandlerCount { get; }
    bool BuildFactory(out string? errorString);
    Task<INpOnWrapperResult?> SendAsync<TRequest>(string channel, TRequest request);
}
