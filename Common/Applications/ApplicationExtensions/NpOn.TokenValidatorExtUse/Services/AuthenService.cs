using Common.Extensions.NpOn.CommonMode;
using NpOn.Protos.Account.V1;

namespace Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;

public class AuthenService(
    AuthenticationProtoService.AuthenticationProtoServiceClient authenticationProtoClient
)
{
    /// <param name="key">sessionId</param>
    /// <returns></returns>
    public AccountLoginItemProto? GetLoginInfoSync(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        var loginInfo = GetLogonInfoBySessionId(key).GetAwaiter().GetResult();
        return loginInfo;
    }

    public async Task<AccountLoginItemProto?> GetLogonInfoBySessionId(string sessionId)
    {
        var logonResponse = await authenticationProtoClient.GetLogonTokenBySessionIdAsync(
            new AccountSessionRequestProto
            {
                SessionId = sessionId.AsDefaultString(),
            });
        if (!logonResponse.Status || logonResponse.Data == null)
            return null;
        return logonResponse.Data;
    }

    public async Task<bool> CheckLogonPermissionExceptionControllers
        (string accountId, string controllerCode, bool isHasBasePermission)
    {
        var logonPermissionExceptionControllers =
            await authenticationProtoClient.IsValidLogonPermissionExceptionControllersAsync(
                new AccountPermissionExceptionCheckRequestProto
                {
                    AccountId = accountId.AsDefaultString(),
                    ControllerCode = controllerCode,
                    IsHasBasePermission = isHasBasePermission,
                });
        return logonPermissionExceptionControllers.Status & logonPermissionExceptionControllers.Data;
    }
}