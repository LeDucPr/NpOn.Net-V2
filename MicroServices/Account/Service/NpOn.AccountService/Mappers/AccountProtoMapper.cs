using Common.Extensions.NpOn.CommonGrpcContract;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Commands;
using MicroServices.Account.Contracts.NpOn.AccountServiceCommand.Queries;
using MicroServices.Account.Contracts.NpOn.AccountServiceReadModel.ReadModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using NpOn.Protos.Account.V1;
using Riok.Mapperly.Abstractions;

namespace MicroServices.Account.Service.NpOn.AccountService.Mappers;

/// <summary>
/// Compile-time zero-reflection mapper between AccountService internal contracts and NpOn.Account.Proto DTOs.
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class AccountProtoMapper
{
    private static long ToUnixMs(DateTime dt) =>
        new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static DateTime? FromUnixMs(bool hasValue, long unixMs) =>
        hasValue ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime : null;

    private static Guid ParseGuidOrEmpty(string? value) =>
        Guid.TryParse(value, out var g) ? g : Guid.Empty;

    private static Guid? ParseNullableGuid(bool hasValue, string? value) =>
        hasValue && Guid.TryParse(value, out var g) ? g : null;

    // --- Common Envelopes ---

    public static CommonStatusReplyProto ToStatusReply(CommonResponse source)
    {
        var reply = new CommonStatusReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        return reply;
    }

    public static BoolReplyProto ToBoolReply(CommonResponse<bool> source)
    {
        var reply = new BoolReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime),
            Data = source.Data
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        return reply;
    }

    public static StringReplyProto ToStringReply(CommonResponse<string> source)
    {
        var reply = new StringReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data != null) reply.Data = source.Data;
        return reply;
    }

    // --- Authentication ---

    public static AccountSignupCommand ToSignupCommand(AccountSignupRequestProto r) => new()
    {
        Email = r.Email,
        PhoneNumber = r.PhoneNumber,
        UserName = r.UserName,
        Password = r.Password,
        LoginType = r.HasLoginType ? (ELoginType)r.LoginType : ELoginType.Default,
        AuthType = (EAuthentication)r.AuthType,
        ClientId = r.ClientId,
        SignupIp = r.HasSignupIp ? r.SignupIp : null,
        DeviceSignupInfo = r.HasDeviceSignupInfo ? r.DeviceSignupInfo : null,
        AuthenApplicationId = r.HasAuthenApplicationId ? r.AuthenApplicationId : null,
        FullName = r.FullName,
        AvatarUrl = r.HasAvatarUrl ? r.AvatarUrl : null,
        ProcessUId = r.HasProcessUid ? r.ProcessUid : null
    };

    public static AccountSetStatusCommand ToSetStatusCommand(AccountSetStatusRequestProto r) => new()
    {
        AccountId = ParseGuidOrEmpty(r.AccountId),
        AccountStatus = (EAccountStatus)r.AccountStatus,
        ProcessUId = r.HasProcessUid ? r.ProcessUid : null
    };

    public static AccountChangePasswordCommand ToChangePasswordCommand(AccountChangePasswordRequestProto r) => new()
    {
        AccountId = ParseGuidOrEmpty(r.AccountId),
        NewPassword = r.NewPassword,
        ProcessUId = r.HasProcessUid ? r.ProcessUid : null
    };

    public static AccountLoginQuery ToLoginQuery(AccountLoginRequestProto r) => new()
    {
        Email = r.HasEmail ? r.Email : null,
        PhoneNumber = r.HasPhoneNumber ? r.PhoneNumber : null,
        UserName = r.HasUserName ? r.UserName : null,
        Password = r.HasPassword ? r.Password : null,
        LoginType = r.HasLoginType ? (ELoginType)r.LoginType : ELoginType.Default,
        AuthType = (EAuthentication)r.AuthType,
        ClientId = r.ClientId,
        Ip = r.HasIp ? r.Ip : null,
        DeviceLoginInfo = r.HasDeviceLoginInfo ? r.DeviceLoginInfo : null,
        AuthenApplicationId = r.HasAuthenApplicationId ? r.AuthenApplicationId : null,
        IsEnableMultiDevice = r.IsEnableMultiDevice,
        ProcessUId = r.HasProcessUid ? r.ProcessUid : null
    };

    public static AccountRefreshTokenQuery ToRefreshTokenQuery(AccountRefreshTokenRequestProto r) => new()
    {
        RefreshToken = r.RefreshToken,
        DeviceInfo = r.HasDeviceInfo ? r.DeviceInfo : null,
        LoginType = r.HasLoginType ? (ELoginType)r.LoginType : null,
        AuthType = (EAuthentication)r.AuthType,
        SessionId = r.SessionId,
        ProcessUId = r.HasProcessUid ? r.ProcessUid : null
    };

    public static AccountLoginReplyProto ToLoginReply(CommonResponse<AccountLoginRModel> source)
    {
        var reply = new AccountLoginReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data != null) reply.Data = ToLoginItem(source.Data);
        return reply;
    }

    public static AccountLoginItemProto ToLoginItem(AccountLoginRModel src)
    {
        var item = new AccountLoginItemProto
        {
            AccountId = src.AccountId.ToString(),
            UserName = src.UserName,
            Password = src.Password,
            AuthType = (int)src.AuthType,
            LoginType = (int)src.LoginType,
            SessionId = src.SessionId,
            MinuteExpire = src.MinuteExpire,
            TokenStatus = (int)src.TokenStatus
        };
        if (src.Id.HasValue) item.Id = src.Id.Value.ToString();
        if (src.Permission.HasValue) item.Permission = (long)src.Permission.Value;
        if (src.FullName != null) item.FullName = src.FullName;
        if (src.PhoneNumber != null) item.PhoneNumber = src.PhoneNumber;
        if (src.DeviceId != null) item.DeviceId = src.DeviceId;
        if (src.Token != null) item.Token = src.Token;
        if (src.RefreshToken != null) item.RefreshToken = src.RefreshToken;
        if (src.CreatedAt.HasValue) item.CreatedAtUnixMs = ToUnixMs(src.CreatedAt.Value);
        if (src.UpdatedAt.HasValue) item.UpdatedAtUnixMs = ToUnixMs(src.UpdatedAt.Value);
        if (src.Email != null) item.Email = src.Email;
        if (src.AvatarUrl != null) item.AvatarUrl = src.AvatarUrl;
        return item;
    }

    // --- Account Info & Addresses ---

    public static AccountInfoAddOrChangeCommand ToInfoAddOrChangeCommand(AccountInfoAddOrChangeRequestProto r) => new()
    {
        AccountId = r.AccountId,
        FullName = r.HasFullName ? r.FullName : null,
        DateOfBirth = FromUnixMs(r.HasDateOfBirthUnixMs, r.DateOfBirthUnixMs),
        Gender = r.HasGender ? (EAccountGender)r.Gender : null,
        Occupation = r.HasOccupation ? r.Occupation : null,
        MaritalStatus = r.HasMaritalStatus ? r.MaritalStatus : null,
        Bio = r.HasBio ? r.Bio : null,
        Website = r.HasWebsite ? r.Website : null,
        SocialLinks = r.HasSocialLinks ? r.SocialLinks : null,
        IdentificationNumber = r.HasIdentificationNumber ? r.IdentificationNumber : null,
        PassportNumber = r.HasPassportNumber ? r.PassportNumber : null,
        TaxCode = r.HasTaxCode ? r.TaxCode : null,
        CompanyName = r.HasCompanyName ? r.CompanyName : null,
        CompanyAddress = r.HasCompanyAddress ? r.CompanyAddress : null,
        CountryId = ParseNullableGuid(r.HasCountryId, r.CountryId),
        ProvinceId = ParseNullableGuid(r.HasProvinceId, r.ProvinceId),
        DistrictId = ParseNullableGuid(r.HasDistrictId, r.DistrictId),
        WardId = ParseNullableGuid(r.HasWardId, r.WardId),
        AddressLine = r.HasAddressLine ? r.AddressLine : null
    };

    public static AccountAddressAddOrChangeCommand ToAddressCommand(AccountAddressAddOrChangeItemProto r) => new()
    {
        AccountId = ParseGuidOrEmpty(r.AccountId),
        CountryId = ParseNullableGuid(r.HasCountryId, r.CountryId),
        ProvinceId = ParseNullableGuid(r.HasProvinceId, r.ProvinceId),
        DistrictId = ParseNullableGuid(r.HasDistrictId, r.DistrictId),
        WardId = ParseNullableGuid(r.HasWardId, r.WardId),
        AddressLine = r.HasAddressLine ? r.AddressLine : null,
        AddressType = (EAddressType)r.AddressType
    };

    public static AccountInfoSingleReplyProto ToInfoSingleReply(CommonResponse<AccountInfoRModel?> source)
    {
        var reply = new AccountInfoSingleReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data != null) reply.Data = ToInfoItem(source.Data);
        return reply;
    }

    public static AccountInfoListReplyProto ToInfoListReply(CommonResponse<AccountInfoRModel[]?> source)
    {
        var reply = new AccountInfoListReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data is { Length: > 0 })
        {
            foreach (var item in source.Data) reply.Data.Add(ToInfoItem(item));
        }
        return reply;
    }

    public static AccountInfoItemProto ToInfoItem(AccountInfoRModel src)
    {
        var item = new AccountInfoItemProto
        {
            AccountId = src.AccountId.ToString(),
            Status = src.Status,
            Id = src.Id.ToString()
        };
        if (src.CountryId.HasValue) item.CountryId = src.CountryId.Value.ToString();
        if (src.ProvinceId.HasValue) item.ProvinceId = src.ProvinceId.Value.ToString();
        if (src.DistrictId.HasValue) item.DistrictId = src.DistrictId.Value.ToString();
        if (src.WardId.HasValue) item.WardId = src.WardId.Value.ToString();
        if (src.FullName != null) item.FullName = src.FullName;
        if (src.DateOfBirth.HasValue) item.DateOfBirthUnixMs = ToUnixMs(src.DateOfBirth.Value);
        if (src.Gender.HasValue) item.Gender = (int)src.Gender.Value;
        if (src.Address != null) item.Address = src.Address;
        if (src.Occupation != null) item.Occupation = src.Occupation;
        if (src.MaritalStatus != null) item.MaritalStatus = src.MaritalStatus;
        if (src.Bio != null) item.Bio = src.Bio;
        if (src.Website != null) item.Website = src.Website;
        if (src.SocialLinks != null) item.SocialLinks = src.SocialLinks;
        if (src.IdentificationNumber != null) item.IdentificationNumber = src.IdentificationNumber;
        if (src.PassportNumber != null) item.PassportNumber = src.PassportNumber;
        if (src.TaxCode != null) item.TaxCode = src.TaxCode;
        if (src.CompanyName != null) item.CompanyName = src.CompanyName;
        if (src.CompanyAddress != null) item.CompanyAddress = src.CompanyAddress;
        if (src.CreatedAt.HasValue) item.CreatedAtUnixMs = ToUnixMs(src.CreatedAt.Value);
        if (src.UpdatedAt.HasValue) item.UpdatedAtUnixMs = ToUnixMs(src.UpdatedAt.Value);
        if (src.Addresses is { Length: > 0 })
        {
            foreach (var addr in src.Addresses) item.Addresses.Add(ToAddressItem(addr));
        }
        return item;
    }

    private static AccountAddressItemProto ToAddressItem(AccountAddressRModel src)
    {
        var item = new AccountAddressItemProto
        {
            Id = src.Id.ToString(),
            AccountId = src.AccountId.ToString(),
            AddressType = (int)src.AddressType
        };
        if (src.CountryId.HasValue) item.CountryId = src.CountryId.Value.ToString();
        if (src.ProvinceId.HasValue) item.ProvinceId = src.ProvinceId.Value.ToString();
        if (src.DistrictId.HasValue) item.DistrictId = src.DistrictId.Value.ToString();
        if (src.WardId.HasValue) item.WardId = src.WardId.Value.ToString();
        if (src.AddressLine != null) item.AddressLine = src.AddressLine;
        if (src.CreatedAt.HasValue) item.CreatedAtUnixMs = ToUnixMs(src.CreatedAt.Value);
        if (src.UpdatedAt.HasValue) item.UpdatedAtUnixMs = ToUnixMs(src.UpdatedAt.Value);
        return item;
    }

    // --- Account Group ---

    public static AccountGroupAddOrChangeCommand ToGroupAddOrChangeCommand(AccountGroupAddOrChangeRequestProto r) => new()
    {
        GroupId = ParseNullableGuid(r.HasGroupId, r.GroupId),
        Leader = ParseGuidOrEmpty(r.Leader),
        Members = r.Members.Count > 0 ? r.Members.Select(ParseGuidOrEmpty).ToArray() : null,
        GroupName = r.HasGroupName ? r.GroupName : null,
        GroupTypes = r.GroupTypes.Select(x => (EAccountGroupType)x).ToArray()
    };

    public static AccountGroupCopyCommand ToGroupCopyCommand(AccountGroupCopyRequestProto r) => new()
    {
        GroupIdNeedCopy = ParseGuidOrEmpty(r.GroupIdNeedCopy),
        Components = r.Components.Select(c => new AccountGroupCopyComponentCommand
        {
            Leader = ParseGuidOrEmpty(c.Leader),
            GroupName = c.HasGroupName ? c.GroupName : null,
            MemberExcludes = c.MemberExcludes.Count > 0 ? c.MemberExcludes.Select(ParseGuidOrEmpty).ToArray() : null,
            MemberAdds = c.MemberAdds.Count > 0 ? c.MemberAdds.Select(ParseGuidOrEmpty).ToArray() : null,
            GroupTypes = c.GroupTypes.Select(x => (EAccountGroupType)x).ToArray()
        }).ToArray()
    };

    public static AccountGroupSearchQuery ToGroupSearchQuery(AccountGroupSearchRequestProto r) => new()
    {
        Keyword = r.HasKeyword ? r.Keyword : null,
        Leader = r.HasLeader ? r.Leader : null,
        PageSize = r.PageSize,
        PageIndex = r.PageIndex
    };

    public static AccountGroupDeleteCommand ToGroupDeleteCommand(AccountGroupDeleteRequestProto r) => new()
    {
        GroupId = ParseGuidOrEmpty(r.GroupId),
        Leader = ParseGuidOrEmpty(r.Leader),
        Members = r.Members.Count > 0 ? r.Members.Select(ParseGuidOrEmpty).ToArray() : null,
        GroupName = r.HasGroupName ? r.GroupName : null
    };

    // --- Account Permission ---

    public static AccountPermissionExceptionListReplyProto ToPermissionExceptionReply(
        CommonResponse<AccountPermissionExceptionRModel[]?> source)
    {
        var reply = new AccountPermissionExceptionListReplyProto
        {
            Status = source.Status,
            Version = source.Version,
            ServerTimeUnixMs = ToUnixMs(source.ServerTime)
        };
        if (source.ErrorCode.HasValue) reply.ErrorCode = (int)source.ErrorCode.Value;
        if (source.TotalRow.HasValue) reply.TotalRow = source.TotalRow.Value;
        if (source.ErrorMessages is { Count: > 0 }) reply.ErrorMessages.AddRange(source.ErrorMessages);
        if (source.Data is { Length: > 0 })
        {
            foreach (var item in source.Data)
            {
                reply.Data.Add(ToPermissionExceptionItem(item));
            }
        }
        return reply;
    }

    [MapProperty(nameof(AccountPermissionExceptionRModel.AccessPermission), nameof(AccountPermissionExceptionItemProto.AccessPermission))]
    public static partial AccountPermissionExceptionItemProto ToPermissionExceptionItem(
        AccountPermissionExceptionRModel source);

    private static string MapGuid(Guid g) => g.ToString();
    private static int MapAccessPermission(EPermissionAccessController p) => (int)p;
}
