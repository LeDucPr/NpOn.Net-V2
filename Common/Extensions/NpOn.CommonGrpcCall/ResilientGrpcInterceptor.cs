using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace NpOn.CommonGrpcCall;

/// <summary>
/// Interceptor providing fault tolerance, graceful degradation, and Saga Outbox fallback
/// for code-first and contract-first gRPC client invocations.
/// </summary>
public class ResilientGrpcInterceptor : Interceptor
{
    private readonly ILogger<ResilientGrpcInterceptor> _logger;
    private readonly ResilientGrpcClientOptions _options;

    public string? Host { get; set; }

    public ResilientGrpcInterceptor(
        ILogger<ResilientGrpcInterceptor> logger,
        ResilientGrpcClientOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new ResilientGrpcClientOptions();
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        var call = continuation(request, context);

        if (!_options.AllowGracefulDegradation)
        {
            return call;
        }

        var interceptedTask = HandleUnaryCallAsync(context.Method.ServiceName, context.Method.Name, request, call.ResponseAsync);

        return new AsyncUnaryCall<TResponse>(
            interceptedTask,
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> HandleUnaryCallAsync<TRequest, TResponse>(
        string serviceName,
        string methodName,
        TRequest request,
        Task<TResponse> responseTask)
    {
        try
        {
            return await responseTask.ConfigureAwait(false);
        }
        catch (RpcException rpcEx) when (
            rpcEx.StatusCode is StatusCode.Unavailable
            or StatusCode.DeadlineExceeded
            or StatusCode.ResourceExhausted
            or StatusCode.Unknown)
        {
            return HandleFailure<TRequest, TResponse>(serviceName, methodName, request, rpcEx);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException)
        {
            return HandleFailure<TRequest, TResponse>(serviceName, methodName, request, ex);
        }
    }

    private TResponse HandleFailure<TRequest, TResponse>(
        string serviceName,
        string methodName,
        TRequest request,
        Exception ex)
    {
        _logger.LogWarning(
            ex,
            "[GRPC RESILIENT DEGRADATION] Downstream service '{ServiceName}/{MethodName}' at '{Host}' unavailable: {Message}. Graceful degradation active.",
            serviceName,
            methodName,
            Host ?? "unspecified",
            ex.Message);

        if (_options.EnableSagaOutboxFallback)
        {
            try
            {
                _options.OnSagaOutboxRecorded?.Invoke(serviceName, methodName, request);
                _logger.LogInformation(
                    "[SAGA OUTBOX FALLBACK] Successfully buffered RPC request for '{ServiceName}/{MethodName}' into Saga Outbox.",
                    serviceName,
                    methodName);
            }
            catch (Exception outboxEx)
            {
                _logger.LogError(
                    outboxEx,
                    "[SAGA OUTBOX ERROR] Failed to record Outbox fallback for '{ServiceName}/{MethodName}'.",
                    serviceName,
                    methodName);
            }
        }

        if (_options.FallbackFactory != null)
        {
            var fallback = _options.FallbackFactory(serviceName, methodName, request);
            if (fallback is TResponse typedFallback)
            {
                return typedFallback;
            }
        }

        return default!;
    }
}

