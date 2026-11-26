using System.Security.Cryptography;
using System.Text;
using Common.Applications.ApplicationsExtensions.NpOn.TokenValidatorExtUse.Services;
using Grpc.Core;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using MicroServices.Account.Definitions.NpOn.ShareAccountConstant;
using NpOn.Protos.Account.V1;
using NpOn.Protos.Sso.V1;

namespace Controllers.NpOn.SSO.Services;

/// <summary>
/// Schema-First public gRPC endpoint exposed by NpOn.SSO (replaces Code-First IAccountGrpcController).
/// </summary>
public sealed class SsoAccountProtoGrpcService(
    AuthenticationProtoService.AuthenticationProtoServiceClient authenticationProtoClient,
    ContextService contextService)
    : SsoAccountProtoService.SsoAccountProtoServiceBase
{
    public override async Task<SsoAccountLoginResponseProto> Login(
        AccountLoginRequest request,
        ServerCallContext context)
    {
        bool isUseMultiDevice = !string.IsNullOrWhiteSpace(request.DeviceInfo) &&
                                (EAuthentication)request.AuthType == EAuthentication.WebApp;
        var protoQuery = new AccountLoginQuery
        {
            ClientId = contextService.ClientId,
            Password = CreateHashPassword(request.Password),
            AuthType = request.AuthType,
            IsEnableMultiDevice = isUseMultiDevice
        };
        if (request.HasEmail) protoQuery.Email = request.Email;
        if (request.HasPhoneNumber) protoQuery.PhoneNumber = request.PhoneNumber;
        if (request.HasUserName) protoQuery.UserName = request.UserName;
        if (request.HasDeviceInfo) protoQuery.DeviceLoginInfo = request.DeviceInfo;
        if (request.HasLoginType) protoQuery.LoginType = request.LoginType;
        var ip = contextService.GetIp();
        if (!string.IsNullOrEmpty(ip)) protoQuery.Ip = ip;
        if (request.HasAppId) protoQuery.AuthenApplicationId = request.AppId;

        var res = await authenticationProtoClient.LoginAsync(protoQuery);
        var reply = new SsoAccountLoginResponseProto
        {
            Status = res.Status,
            Version = res.Version,
            ServerTimeUnixMs = res.ServerTimeUnixMs
        };
        if (res.HasErrorCode) reply.ErrorCode = res.ErrorCode;
        reply.ErrorMessages.AddRange(res.ErrorMessages);
        if (res.Data != null)
        {
            var m = new AccountLoginOutputModel
            {
                AccountId = res.Data.AccountId,
                AuthType = res.Data.AuthType,
                LoginType = res.Data.LoginType,
                SessionId = res.Data.SessionId,
                MinuteExpire = res.Data.MinuteExpire
            };
            if (res.Data.HasFullName) m.FullName = res.Data.FullName;
            if (res.Data.HasPhoneNumber) m.PhoneNumber = res.Data.PhoneNumber;
            if (res.Data.HasToken) m.Token = res.Data.Token;
            if (res.Data.HasRefreshToken) m.RefreshToken = res.Data.RefreshToken;
            if (res.Data.HasCreatedAtUnixMs) m.CreatedAtUnixMs = res.Data.CreatedAtUnixMs;
            reply.Model = m;
        }
        return reply;
    }

    private static string CreateHashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return string.Empty;
        var md5Hex = Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(password))).ToLowerInvariant();
        byte[] salt = Encoding.UTF8.GetBytes(ContextServiceCode.DefaultSaltPassword);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(md5Hex, salt, 369, HashAlgorithmName.SHA1, 20);
        return Convert.ToBase64String(hash);
    }
}
