using Common.Extensions.NpOn.HeaderConfig;
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
    private readonly GrpcHeaderConfig _headerConfig = headerConfig;

    protected override void WriteHeader()
    {
        string? sessionKey = httpContextAccessor?.HttpContext?.Request.Headers.FirstOrDefault(x =>
                x.Key.Equals(DefaultHeaderConstant.GrpcInternalCallerSessionCode,
                    StringComparison.CurrentCultureIgnoreCase))
            .Value;
        if (sessionKey?.Length > 0)
            _headerConfig.Replace(DefaultHeaderConstant.GrpcInternalCallerSessionCode, sessionKey);

        string? authenKey = httpContextAccessor?.HttpContext?.Request.Headers.FirstOrDefault(x =>
                x.Key.Equals(DefaultHeaderConstant.GrpcInteralCallerAuthentication,
                    StringComparison.CurrentCultureIgnoreCase))
            .Value;
        if (authenKey?.Length > 0)
            _headerConfig.Replace(DefaultHeaderConstant.GrpcInteralCallerAuthentication, authenKey);

        // Forward External Authorization Header (e.g. Bearer token) if present
        string? authorizationExt = httpContextAccessor?.HttpContext?.Request.Headers.FirstOrDefault(x =>
                x.Key.Equals("Authorization", StringComparison.CurrentCultureIgnoreCase))
            .Value;
        // Grpc header keys should be lower case, gRPC requires lowercase header keys.
        if (authorizationExt?.Length > 0)
            _headerConfig.Replace("authorization", authorizationExt);
    }
}