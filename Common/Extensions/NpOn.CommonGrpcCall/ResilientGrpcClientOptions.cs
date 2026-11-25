namespace NpOn.CommonGrpcCall;

/// <summary>
/// Configuration options for resilient and fault-tolerant gRPC client connections.
/// Allows microservices to start and execute gracefully even when downstream services are offline or not registered.
/// </summary>
public class ResilientGrpcClientOptions
{
    /// <summary>
    /// When true, allows the service to boot and operate normally even if the target gRPC URL is not configured or missing from DI.
    /// </summary>
    public bool IsOptional { get; set; } = true;

    /// <summary>
    /// When true, catches RpcException (Unavailable, DeadlineExceeded, SocketExceptions) and returns a default/fallback response
    /// instead of crashing the calling service or aborting the HTTP request.
    /// </summary>
    public bool AllowGracefulDegradation { get; set; } = true;

    /// <summary>
    /// When true, captures failed RPC calls into the Saga Transactional Outbox for asynchronous dispatch once connectivity is restored.
    /// </summary>
    public bool EnableSagaOutboxFallback { get; set; } = false;

    /// <summary>
    /// Maximum number of automatic retry attempts before falling back or degrading.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Custom fallback generator when an RPC call fails and graceful degradation is enabled.
    /// Parameters: (serviceName, methodName, requestPayload) => fallbackResponse
    /// </summary>
    public Func<string, string, object?, object?>? FallbackFactory { get; set; }

    /// <summary>
    /// Callback invoked when an RPC call fails and Saga Outbox fallback is triggered.
    /// </summary>
    public Action<string, string, object?>? OnSagaOutboxRecorded { get; set; }
}

