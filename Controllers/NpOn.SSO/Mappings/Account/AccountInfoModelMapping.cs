using Controllers.NpOn.SSO.OutputModels;
using MicroServices.Account.Definitions.NpOn.AccountEnum;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Mappings.Account;

public static class AccountInfoModelMapping
{
    private static Guid? ParseNullableGuid(bool hasValue, string? value) =>
        hasValue && Guid.TryParse(value, out var g) ? g : null;

    public static AccountInfoDetailOutputModel ToModel(this AccountInfoItemProto accountInfoRModel)
    {
        return new AccountInfoDetailOutputModel()
        {
            Id = Guid.TryParse(accountInfoRModel.Id, out var id) ? id : Guid.Empty,
            AccountId = Guid.TryParse(accountInfoRModel.AccountId, out var accId) ? accId : Guid.Empty,
            CountryId = ParseNullableGuid(accountInfoRModel.HasCountryId, accountInfoRModel.CountryId),
            ProvinceId = ParseNullableGuid(accountInfoRModel.HasProvinceId, accountInfoRModel.ProvinceId),
            DistrictId = ParseNullableGuid(accountInfoRModel.HasDistrictId, accountInfoRModel.DistrictId),
            WardId = ParseNullableGuid(accountInfoRModel.HasWardId, accountInfoRModel.WardId),
            FullName = accountInfoRModel.HasFullName ? accountInfoRModel.FullName : null,
            DateOfBirth = accountInfoRModel.HasDateOfBirthUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(accountInfoRModel.DateOfBirthUnixMs).UtcDateTime
                : null,
            Gender = accountInfoRModel.HasGender ? (EAccountGender)accountInfoRModel.Gender : null,
            Address = accountInfoRModel.HasAddress ? accountInfoRModel.Address : null,
            Occupation = accountInfoRModel.HasOccupation ? accountInfoRModel.Occupation : null,
            MaritalStatus = accountInfoRModel.HasMaritalStatus ? accountInfoRModel.MaritalStatus : null,
            Bio = accountInfoRModel.HasBio ? accountInfoRModel.Bio : null,
            Website = accountInfoRModel.HasWebsite ? accountInfoRModel.Website : null,
            SocialLinks = accountInfoRModel.HasSocialLinks ? accountInfoRModel.SocialLinks : null,
            IdentificationNumber = accountInfoRModel.HasIdentificationNumber ? accountInfoRModel.IdentificationNumber : null,
            PassportNumber = accountInfoRModel.HasPassportNumber ? accountInfoRModel.PassportNumber : null,
            TaxCode = accountInfoRModel.HasTaxCode ? accountInfoRModel.TaxCode : null,
            CompanyName = accountInfoRModel.HasCompanyName ? accountInfoRModel.CompanyName : null,
            CompanyAddress = accountInfoRModel.HasCompanyAddress ? accountInfoRModel.CompanyAddress : null,
            Status = accountInfoRModel.Status,
            CreatedAt = accountInfoRModel.HasCreatedAtUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(accountInfoRModel.CreatedAtUnixMs).UtcDateTime
                : null,
            UpdatedAt = accountInfoRModel.HasUpdatedAtUnixMs
                ? DateTimeOffset.FromUnixTimeMilliseconds(accountInfoRModel.UpdatedAtUnixMs).UtcDateTime
                : null,
        };
    }
}