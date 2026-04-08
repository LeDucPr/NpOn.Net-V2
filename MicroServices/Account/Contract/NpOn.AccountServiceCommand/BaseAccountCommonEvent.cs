using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Events;
using ProtoBuf;

namespace MicroServices.Account.Contracts.NpOn.AccountServiceCommand;

[ProtoContract]
[ProtoInclude(100, typeof(AccountSaveLoginEvent))]
public abstract class BaseAccountCommonEvent : CommonMessageContent
{
}