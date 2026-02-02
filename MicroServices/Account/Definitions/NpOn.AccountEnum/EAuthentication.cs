using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

[Flags]
public enum EAuthentication
{
    [Display(Name = "Mobile")] Mobile = 1,
    [Display(Name = "WebApp")] WebApp = 2,
}