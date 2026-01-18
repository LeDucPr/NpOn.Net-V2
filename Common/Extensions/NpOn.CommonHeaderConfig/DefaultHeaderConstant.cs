namespace Common.Extensions.NpOn.HeaderConfig;

public static class DefaultHeaderConstant
{
    // internal (grpc protocol)
    public const string GrpcInternalCallerUserName = "caller-npon-user"; // client
    public const string GrpcInternalCallerMachineName = "caller-npon-machine"; // machine
    public const string GrpcInternalCallerOsVersion = "caller-npon-os"; // system
    public const string GrpcInternalCallerSessionCode = "caller-npon-ss"; // session
    public static readonly string GrpcInternalCallerSessionCodeDefaultValue = string.Empty;
    
    // general
    public static readonly string GrpcInteralCallerAuthentication = "authenticaion";
}