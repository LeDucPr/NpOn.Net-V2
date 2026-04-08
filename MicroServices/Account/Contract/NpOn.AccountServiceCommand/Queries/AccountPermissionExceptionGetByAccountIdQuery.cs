using ProtoBuf;

namespace MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;

[ProtoContract]
public class AccountPermissionExceptionGetByAccountIdQuery : BaseAccountCommand
{
    [ProtoMember(1)] public required string AccountId { get; set; }
}