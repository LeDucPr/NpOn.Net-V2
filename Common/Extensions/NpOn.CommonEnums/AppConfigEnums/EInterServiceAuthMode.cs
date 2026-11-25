using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.AppConfigEnums;

public enum EInterServiceAuthMode
{
    [Display(Name = "Jwt")]
    Jwt = 0,

    [Display(Name = "Mtls")]
    Mtls = 1,

    [Display(Name = "ServiceMesh")]
    ServiceMesh = 2
}
