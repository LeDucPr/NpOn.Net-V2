using MicroServices.Account.Definitions.NpOn.AccountEnum;
using ProtoBuf;

namespace MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;

[ProtoContract]
public class AccountPermissionExceptionRModel : BaseAccountRModelFromGrpcTable
{
    [ProtoMember(1)] public Guid AccountId { get; set; }
    [ProtoMember(2)] public string ControllerCode { get; set; } = string.Empty;
    [ProtoMember(3)] public EPermissionAccessController AccessPermission { get; set; }

    protected override void FieldMapper()
    {
        FieldMap ??= [];
        FieldMap.Add(nameof(AccountId), "acc_srv_account_id");
        FieldMap.Add(nameof(ControllerCode), "acc_srv_account_permission_controller_code");
        FieldMap.Add(nameof(AccessPermission), "access_permission");
    }
}

/// <summary>
/// Explicit cached permission state per account stored in Redis.
/// An empty Exceptions list is cached intentionally to distinguish a valid
/// zero-exception account from a Redis cache miss (null).
/// </summary>
[ProtoContract]
public sealed class AccountPermissionExceptionCacheState
{
    [ProtoMember(1)] public long CatalogVersion { get; set; }
    [ProtoMember(2)] public long BasePermissionMask { get; set; }
    [ProtoMember(3)] public bool IsAccountActive { get; set; }
    [ProtoMember(4)] public List<AccountPermissionExceptionCacheItem> Exceptions { get; set; } = [];
}

[ProtoContract]
public sealed class AccountPermissionExceptionCacheItem
{
    [ProtoMember(1)] public string ControllerCode { get; set; } = string.Empty;
    [ProtoMember(2)] public EPermissionAccessController AccessPermission { get; set; }
}