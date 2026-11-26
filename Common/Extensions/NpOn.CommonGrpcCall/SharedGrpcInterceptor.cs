using Common.Extensions.NpOn.HeaderConfig;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NpOn.CommonGrpcCall;

public class SharedGrpcInterceptor(
    ILogger<GrpcInterceptorBase> logger,
    GrpcHeaderConfig headerConfig,
    IHttpContextAccessor? httpContextAccessor,
    bool isUseLogUnaryCall = true,
    bool isUseLogClientStreamingCall = true,
    bool isUseLogServerStreamingCall = true,
    bool isUseLogDuplexStreamingCall = true
) : GrpcInterceptorBase(logger, headerConfig, isUseLogUnaryCall, isUseLogClientStreamingCall, isUseLogServerStreamingCall,
    isUseLogDuplexStreamingCall)
{
    protected override void WriteCallHeaders(Metadata headers)
    {
        var requestHeaders = httpContextAccessor?.HttpContext?.Request.Headers;
        if (requestHeaders == null)
            return;

        // Forward session code from HTTP request to per-call gRPC Metadata only
        if (requestHeaders.TryGetValue(DefaultHeaderConstant.GrpcInternalCallerSessionCode, out var sessionValues))
        {
            string? sessionKey = sessionValues.FirstOrDefault();
            if (!string.IsNullOrEmpty(sessionKey))
                UpsertHeader(headers, DefaultHeaderConstant.GrpcInternalCallerSessionCode, sessionKey);
        }

        // Forward External Authorization Header (e.g. Bearer token) to per-call gRPC Metadata only
        if (requestHeaders.TryGetValue("Authorization", out var authValues))
        {
            string? authorizationExt = authValues.FirstOrDefault();
            // gRPC requires lowercase header keys.
            if (!string.IsNullOrEmpty(authorizationExt))
                UpsertHeader(headers, "authorization", authorizationExt);
        }
    }

    private static void UpsertHeader(Metadata headers, string key, string value)
    {
        var existing = headers.Get(key);
        if (existing != null)
            headers.Remove(existing);
        headers.Add(key, value);
    }
}