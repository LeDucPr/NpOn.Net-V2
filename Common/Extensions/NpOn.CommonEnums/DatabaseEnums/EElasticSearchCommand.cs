using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

public enum EElasticSearchCommand : byte
{
    [Display(Name = "Get")] Get = 1,
    [Display(Name = "Search")] Search = 2,
    [Display(Name = "Index")] Index = 3,
    [Display(Name = "Delete")] Delete = 4,

    [Display(Name = "Get Many")] GetMany = 11,
    [Display(Name = "Index Many")] IndexMany = 12,
    [Display(Name = "Delete Many")] DeleteMany = 13,
}
