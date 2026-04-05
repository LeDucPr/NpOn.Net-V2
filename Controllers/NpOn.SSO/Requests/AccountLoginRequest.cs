using MicroServices.Account.Definitions.NpOn.AccountEnum;
using System.Runtime.Serialization;

namespace Controllers.NpOn.SSO.Requests;

public class AccountSignupRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public required string UserName { get; set; }
    public required string Password { get; set; }
    public required string FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public required EAuthentication AuthType { get; set; }
    public string? DeviceInfo { get; set; }
    public string? AppId { get; set; }
}

public class AccountChangeStatusRequest
{
    public required Guid AccountId { get; set; }
    public EAccountStatus AccountStatus { get; set; }
}

public class ChangeAccountPasswordRequest
{
    public required Guid AccountId { get; set; }
    public string? NewPassword { get; set; }
    public EAccountStatus? AccountStatus { get; set; }
}

[DataContract]
public class AccountLoginRequest
{
    [DataMember(Order = 1)] public string? Email { get; set; }
    [DataMember(Order = 2)] public string? PhoneNumber { get; set; }
    [DataMember(Order = 3)] public string? UserName { get; set; }
    [DataMember(Order = 4)] public required string Password { get; set; }
    [DataMember(Order = 5)] public string? DeviceInfo { get; set; }
    [DataMember(Order = 6)] public ELoginType? LoginType { get; set; }
    [DataMember(Order = 7)] public required EAuthentication AuthType { get; set; }
    [DataMember(Order = 8)] public string? AppId { get; set; }
    [DataMember(Order = 9)] public bool? IsOldSystemAccount { get; set; } = false;
}

public class AccountRefreshTokenRequest
{
    public required string RefreshToken { get; set; }
    public string? DeviceInfo { get; set; }
    public ELoginType? LoginType { get; set; }
    public required EAuthentication AuthType { get; set; }
    public string? ReturnUrl { get; set; }
}

public class AccountLogoutRequest
{
    // public string? DeviceInfo { get; set; }
    // public required EAuthentication AuthType { get; set; }
}