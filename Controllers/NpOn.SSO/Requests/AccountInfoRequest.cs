using MicroServices.Account.Definitions.NpOn.AccountEnum;
using NpOn.Protos.Account.V1;

namespace Controllers.NpOn.SSO.Requests;

public class AccountInfoAddOrChangeRequest
{
    public Guid? Id { get; set; }
    public string? FullName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public EAccountGender? Gender { get; set; }
    public string? Occupation { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Bio { get; set; }
    public string? Website { get; set; }
    public string? SocialLinks { get; set; }
    public string? IdentificationNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? TaxCode { get; set; }
    public string? CompanyName { get; set; }
    public string? CompanyAddress { get; set; }

    // address (default)
    public Guid? CountryId { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? DistrictId { get; set; }
    public Guid? WardId { get; set; }
    public string? AddressLine { get; set; }
}

public class AccountAddressesAddOrChangeRequest
{
    public Guid? CountryId { get; set; }
    public Guid? ProvinceId { get; set; }
    public Guid? DistrictId { get; set; }
    public Guid? WardId { get; set; }
    public string? AddressLine { get; set; }
    public EAddressType AddressType { get; set; }
}

public static class AccountInfoAddOrChangeRequestExtensions
{
    public static AccountInfoAddOrChangeCommand ToProtoCommand(
        this AccountInfoAddOrChangeRequest request,
        string accountIdString)
    {
        var cmd = new AccountInfoAddOrChangeCommand
        {
            AccountId = accountIdString
        };
        if (request.FullName != null) cmd.FullName = request.FullName;
        if (request.DateOfBirth.HasValue)
            cmd.DateOfBirthUnixMs = new DateTimeOffset(DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        if (request.Gender.HasValue) cmd.Gender = (int)request.Gender.Value;
        if (request.Occupation != null) cmd.Occupation = request.Occupation;
        if (request.MaritalStatus != null) cmd.MaritalStatus = request.MaritalStatus;
        if (request.Bio != null) cmd.Bio = request.Bio;
        if (request.Website != null) cmd.Website = request.Website;
        if (request.SocialLinks != null) cmd.SocialLinks = request.SocialLinks;
        if (request.IdentificationNumber != null) cmd.IdentificationNumber = request.IdentificationNumber;
        if (request.PassportNumber != null) cmd.PassportNumber = request.PassportNumber;
        if (request.TaxCode != null) cmd.TaxCode = request.TaxCode;
        if (request.CompanyName != null) cmd.CompanyName = request.CompanyName;
        if (request.CompanyAddress != null) cmd.CompanyAddress = request.CompanyAddress;
        if (request.CountryId.HasValue) cmd.CountryId = request.CountryId.Value.ToString();
        if (request.ProvinceId.HasValue) cmd.ProvinceId = request.ProvinceId.Value.ToString();
        if (request.DistrictId.HasValue) cmd.DistrictId = request.DistrictId.Value.ToString();
        if (request.WardId.HasValue) cmd.WardId = request.WardId.Value.ToString();
        if (request.AddressLine != null) cmd.AddressLine = request.AddressLine;
        return cmd;
    }

    public static AccountAddressAddOrChangeCommand ToProtoCommand(
        this AccountAddressesAddOrChangeRequest request,
        Guid accountId)
    {
        var cmd = new AccountAddressAddOrChangeCommand
        {
            AccountId = accountId.ToString(),
            AddressType = (int)request.AddressType
        };
        if (request.CountryId.HasValue) cmd.CountryId = request.CountryId.Value.ToString();
        if (request.ProvinceId.HasValue) cmd.ProvinceId = request.ProvinceId.Value.ToString();
        if (request.DistrictId.HasValue) cmd.DistrictId = request.DistrictId.Value.ToString();
        if (request.WardId.HasValue) cmd.WardId = request.WardId.Value.ToString();
        if (request.AddressLine != null) cmd.AddressLine = request.AddressLine;
        return cmd;
    }
}