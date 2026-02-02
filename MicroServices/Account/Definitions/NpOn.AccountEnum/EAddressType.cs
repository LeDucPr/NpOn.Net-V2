using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

public enum EAddressType
{
    [Display(Name = "Other")] Other = 0, 
    [Display(Name = "Default")] Default = 1, 
}