using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

[Flags]
public enum EGrpcEndUseType : byte
{
    [Display(Name = "CallToInternalServer")] CallToInternalServer = 1 << 0,
    [Display(Name = "CallToExternalServer")] CallToExternalServer = 1 << 1,
    [Display(Name = "Client")] Client = 1 << 2,
}