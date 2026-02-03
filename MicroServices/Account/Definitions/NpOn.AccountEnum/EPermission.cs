using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

[Flags]
public enum EPermission
{
    [Display(Name = "Unknown")] Unknown = 0,

    [Display(Name = "SystemAdministrator")] SystemAdministrator = 1 << 0,
    [Display(Name = "Administrator")] Administrator = 1 << 1,
    [Display(Name = "User")] User = 1 << 6,
    
}

public enum EPermissionAccessController
{
    [Display(Name = "Enable")] Enable = 1,
    [Display(Name = "Disable")] Disable = 2,
}