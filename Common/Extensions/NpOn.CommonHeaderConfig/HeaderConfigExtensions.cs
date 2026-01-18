using Common.Extensions.NpOn.CommonEnums;

namespace Common.Extensions.NpOn.HeaderConfig;

public static class HeaderConfigExtensions
{
    public static IHeaderConfig? InitGrpcHeaderConfig(
        EGrpcEndUseType endUseType,
        Dictionary<string, string>? headers = null
    ) => endUseType switch
    {
        EGrpcEndUseType.ExternalServer when headers is not { Count: > 0 } => null,

        EGrpcEndUseType.InternalServer or
            EGrpcEndUseType.ExternalServer or
            EGrpcEndUseType.Client => new GrpcHeaderConfig(endUseType, headers),

        _ => null
    };
}