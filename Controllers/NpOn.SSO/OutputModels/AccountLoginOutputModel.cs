using MicroServices.Account.Definitions.NpOn.AccountEnum;
using System.Runtime.Serialization;

namespace Controllers.NpOn.SSO.OutputModels;

[DataContract]
public class AccountLoginOutputModel
{
    [DataMember(Order = 1)] public required Guid AccountId { get; set; }
    [DataMember(Order = 2)] public required EAuthentication AuthType { get; set; }
    [DataMember(Order = 3)] public required ELoginType LoginType { get; set; }
    [DataMember(Order = 4)] public string? FullName { get; set; }
    [DataMember(Order = 5)] public string? PhoneNumber { get; set; }
    [DataMember(Order = 6)] public string? Token { get; set; }
    [DataMember(Order = 7)] public string? RefreshToken { get; set; }
    [DataMember(Order = 8)] public DateTime? CreatedAt { get; set; }
    [DataMember(Order = 9)] public required string SessionId { get; set; }
    [DataMember(Order = 10)] public int MinuteExpire { get; set; }
}