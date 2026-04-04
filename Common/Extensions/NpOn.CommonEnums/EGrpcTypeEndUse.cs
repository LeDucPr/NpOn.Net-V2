using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

[Flags]
public enum EGrpcEndUseType : byte
{
    [Display(Name = "CallToInternalServer")] InternalServer = 1 << 0, // internal service
    [Display(Name = "CallToExternalServer")] ExternalServer = 1 << 1, // export service global (standard)
    [Display(Name = "Client")] Client = 1 << 2, // as Client (server - server with this service is client)
}