using Controllers.NpOn.SSO.OutputModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Mappings.Account;

public static class AccountModelMapping
{
    public static AccountLoginOutputModel ToModel(this AccountLoginRModel accountLogin)
    {
        return new AccountLoginOutputModel
        {
            AccountId = Guid.TryParse(accountLogin.AccountId, out var id) ? id : Guid.Empty,
            AuthType = (EAuthentication)accountLogin.AuthType,
            LoginType = (ELoginType)accountLogin.LoginType,
            FullName = accountLogin.HasFullName ? accountLogin.FullName : null,
            PhoneNumber = accountLogin.HasPhoneNumber ? accountLogin.PhoneNumber : null,
            Token = accountLogin.HasToken ? accountLogin.Token : null,
            RefreshToken = accountLogin.HasRefreshToken ? accountLogin.RefreshToken : null,
            CreatedAt = accountLogin.HasCreatedAtUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(accountLogin.CreatedAtUnixMs).UtcDateTime
                : null,
            SessionId = accountLogin.SessionId,
            MinuteExpire = accountLogin.MinuteExpire,
        };
    }
}
