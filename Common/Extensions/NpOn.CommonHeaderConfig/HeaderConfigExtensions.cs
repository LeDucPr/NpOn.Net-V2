using Common.Extensions.NpOn.CommonEnums;

namespace Common.Extensions.NpOn.HeaderConfig;

public static class HeaderConfigExtensions
{
    public static GrpcHeaderConfig? InitGrpcHeaderConfig(
        EGrpcEndUseType endUseType,
        Dictionary<string, string>? headers = null
    ) => endUseType switch
    {
        EGrpcEndUseType.CallToExternalServer when headers is not { Count: > 0 } => null,

        EGrpcEndUseType.CallToInternalServer or
            EGrpcEndUseType.CallToExternalServer or
            EGrpcEndUseType.Client => new GrpcHeaderConfig(endUseType, headers),

        _ => null
    };
}