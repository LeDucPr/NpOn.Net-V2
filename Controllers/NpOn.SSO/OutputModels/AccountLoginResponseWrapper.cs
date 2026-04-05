using System.Runtime.Serialization;

namespace Controllers.NpOn.SSO.OutputModels;

[DataContract]
public class AccountLoginResponseWrapper
{
    [DataMember(Order = 1)]
    public AccountLoginOutputModel Model { get; set; } = null!;
}
