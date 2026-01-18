using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

[Flags]
public enum EGrpcEndUseType : byte
{
    [Display(Name = "InternalServer")] InternalServer = 1 << 0,
    [Display(Name = "ExternalServer")] ExternalServer = 1 << 1,
    [Display(Name = "Client")] Client = 1 << 2,
}