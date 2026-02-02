using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

[Flags]
public enum ELoginType
{
    [Display(Name = "Default")] Default = 1,
}