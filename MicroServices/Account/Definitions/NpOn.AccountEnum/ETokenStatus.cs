using System.ComponentModel.DataAnnotations;

namespace MicroServices.Account.Definitions.NpOn.AccountEnum;

public enum ETokenStatus
{
    [Display(Name = "Inactive")] Inactive = 0, // đã hết hạn
    [Display(Name = "Active")] Active = 1, // đang hoạt động 
}