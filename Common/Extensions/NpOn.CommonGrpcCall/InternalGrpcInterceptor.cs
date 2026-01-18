using Common.Extensions.NpOn.HeaderConfig;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NpOn.CommonGrpcCall;

public class InternalGrpcInterceptor(
    ILogger<GrpcInterceptor> logger,
    GrpcHeaderConfig headerConfig,
    IHttpContextAccessor? httpContextAccessor,
    bool isUseLogUnaryCall = true,
    bool isUseLogClientStreamingCall = true,
    bool isUseLogServerStreamingCall = true,
    bool isUseLogDuplexStreamingCall = true
) : GrpcInterceptor(logger, headerConfig, isUseLogUnaryCall, isUseLogClientStreamingCall, isUseLogServerStreamingCall,
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
    }
}