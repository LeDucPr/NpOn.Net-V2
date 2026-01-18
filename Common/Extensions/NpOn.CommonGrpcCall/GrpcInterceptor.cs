using System.Diagnostics;
using Common.Extensions.NpOn.HeaderConfig;
using Grpc.Core;
using Grpc.Core.Interceptors;
// using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NpOn.CommonGrpcCall;

public abstract class GrpcInterceptor(
    ILogger<GrpcInterceptor> logger,
    GrpcHeaderConfig headerConfig,
    // , IHttpContextAccessor? httpContextAccessor
    bool isUseLogUnaryCall = true,
    bool isUseLogClientStreamingCall = true,
    bool isUseLogServerStreamingCall = true,
    bool isUseLogDuplexStreamingCall = true
) : Interceptor
{
    // Default Host 
    public string? Host { get; set; }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        var call = continuation(request, context);

        if (!isUseLogUnaryCall)
            return call;

        // logger
        return new AsyncUnaryCall<TResponse>(
            LoggingAsyncUnaryReader(context.Method.ServiceName, context.Method.Name,
                call.ResponseAsync),
            call.ResponseHeadersAsync,
            call.GetStatus, call.GetTrailers, call.Dispose);
    }


    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        var call = continuation(context);

        if (!isUseLogClientStreamingCall)
            return call;

        // logger
        return new AsyncClientStreamingCall<TRequest, TResponse>(
            call.RequestStream,
            LoggingAsyncUnaryReader(context.Method.ServiceName, context.Method.Name, call.ResponseAsync),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        var call = continuation(request, context);

        if (!isUseLogServerStreamingCall)
        {
            return call;
        }

        // logger
        return new AsyncServerStreamingCall<TResponse>(
            HandleResponseStream(context.Method.ServiceName, context.Method.Name, call.ResponseStream),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        AddCallerMetadata(ref context);
        var call = continuation(context);

        if (!isUseLogDuplexStreamingCall)
        {
            return call;
        }

        // logger
        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            call.RequestStream,
            HandleResponseStream(context.Method.ServiceName, context.Method.Name, call.ResponseStream),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private IAsyncStreamReader<TResponse> HandleResponseStream<TResponse>(string serviceName, string action,
        IAsyncStreamReader<TResponse> responseStream)
    {
        return new LoggingAsyncStreamReader<TResponse>(responseStream, logger, Host, serviceName, action, LogError);
    }

    protected abstract void WriteHeader(); // WriteHeader (next request)

    private void AddCallerMetadata<TRequest, TResponse>(ref ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        Metadata header = headerConfig.GetHeader() ?? new Metadata();
        WriteHeader();
        var options = context.Options.WithHeaders(header);
        context = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
    }

    private void LogError(Exception exception, string message, string serviceName, string action)
    {
        using (logger.BeginScope(new Dictionary<string, object>
               {
                   { "CallToHost", Host ?? string.Empty },
                   { "CallToServiceName", serviceName },
                   { "CallToAction", action }
               }))
        {
            logger.LogError(exception, "{Message}", message);
        }
    }

    
    private async Task<TResponse> LoggingAsyncUnaryReader<TResponse>(string serviceName, string action, Task<TResponse> t)
    {
        var startTime = Stopwatch.GetTimestamp();
        var actionTracking = $"/{serviceName}/{action}";
        try
        {
            var response = await t;
            var executeTime = Stopwatch.GetElapsedTime(startTime);

            logger.LogInformation("Service Request host {Host} url {ActionTracking} executeTime {ExecuteTime}",
                Host,
                actionTracking,
                executeTime);
            return response;
        }
        catch (Exception ex)
        {
            var executeTime = Stopwatch.GetElapsedTime(startTime);
            LogError(ex,
                $"GRPC call error - ServiceName: {serviceName} - Action: {action} - Duration: {executeTime.TotalMilliseconds:F2}ms - Message: {ex.Message}",
                serviceName,
                action);
            throw;
        }
    }
    
    private class LoggingAsyncStreamReader<T>(
        IAsyncStreamReader<T> inner,
        ILogger logger,
        string? host,
        string serviceName,
        string action,
        Action<Exception, string, string, string> logError) : IAsyncStreamReader<T>
    {
        private readonly long _startTime = Stopwatch.GetTimestamp();
        private bool _isStarted;

        public T Current => inner.Current;

        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var actionTracking = $"/{serviceName}/{action}";
            if (!_isStarted)
            {
                logger.LogInformation("Start Streaming Request host {Host} url {ActionTracking}", host, actionTracking);
                _isStarted = true;
            }

            try
            {
                if (await inner.MoveNext(cancellationToken)) return true;

                var totalTime = Stopwatch.GetElapsedTime(_startTime);
                logger.LogInformation("End Streaming Request host {Host} url {ActionTracking} totalTime {ExecuteTime}",
                    host,
                    actionTracking,
                    totalTime);
                return false;
            }
            catch (Exception ex)
            {
                var executeTime = Stopwatch.GetElapsedTime(_startTime);
                logError(ex,
                    $"GRPC streaming error - ServiceName: {serviceName} - Action: {action} - Duration: {executeTime.TotalMilliseconds:F2}ms - Message: {ex.Message}",
                    serviceName,
                    action);
                throw;
            }
        }
    }
}