using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

[Flags]
public enum EAccountGroupType
{
    [Display(Name = "InvalidGroup")] InvalidGroup = 0,
    [Display(Name = "PermissionGroup")] PermissionGroup = 1 << 0,
}