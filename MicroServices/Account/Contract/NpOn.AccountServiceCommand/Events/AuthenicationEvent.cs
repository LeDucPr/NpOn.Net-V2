using MicroServices.Account.Definitions.NpOn.AccountEnum;
using ProtoBuf;

namespace MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Events;

[ProtoContract]
[ProtoInclude(100, typeof(AccountSaveLogoutEvent))]
public class AccountSaveLoginEvent : BaseAccountCommonEvent
{
    [ProtoMember(1)] public Guid? Id { get; set; }
    [ProtoMember(2)] public required Guid AccountId { get; set; }
    [ProtoMember(3)] public required string UserName { get; set; }
    [ProtoMember(4)] public required string Password { get; set; }
    [ProtoMember(5)] public required EAuthentication AuthType { get; set; }
    [ProtoMember(6)] public required ELoginType LoginType { get; set; }
    [ProtoMember(7)] public EPermission? Permission { get; set; }
    [ProtoMember(8)] public string? FullName { get; set; }
    [ProtoMember(9)] public string? PhoneNumber { get; set; }
    [ProtoMember(10)] public string? DeviceId { get; set; }
    [ProtoMember(11)] public string? Token { get; set; }
    [ProtoMember(12)] public string? RefreshToken { get; set; }
    [ProtoMember(13)] public DateTime? CreatedAt { get; set; }
    [ProtoMember(14)] public DateTime? UpdatedAt { get; set; }
    [ProtoMember(15)] public required string SessionId { get; set; }
    [ProtoMember(16)] public int MinuteExpire { get; set; }
    [ProtoMember(17)] public ETokenStatus TokenStatus { get; set; } = ETokenStatus.Inactive;
    [ProtoMember(18)] public string? Email { get; set; }
    [ProtoMember(19)] public string? AvatarUrl { get; set; }
}

[ProtoContract]
public class AccountSaveLogoutEvent : AccountSaveLoginEvent
{
}