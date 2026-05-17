using Common.Extensions.NpOn.BaseDbFactory.FactoryResults;
using Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

namespace Common.Extensions.NpOn.BaseDbFactory.Broadcasts;

/// <summary>
/// Broadcast -> Handler + Connection(Hold) + EventAdd
/// </summary>
public interface IBaseBroadcastFactoryWrapper : IDisposable
{
    int HandlerCount { get; }
    // static abstract IBaseBroadcastFactory operator +(IBaseBroadcastFactory? factory, BaseBroadcastHandler? handler);
    bool BuildFactory(out string? errorString);
    void DestroyInternal();
}

public static class BroadcastExtensions
{
    public static IBaseBroadcastFactoryWrapper? SelfReset(this IBaseBroadcastFactoryWrapper? factory)
    {
        if (factory == null) 
            return null;
        factory.DestroyInternal();

        GC.Collect();
        GC.WaitForPendingFinalizers(); 
        return factory; 
    }
}