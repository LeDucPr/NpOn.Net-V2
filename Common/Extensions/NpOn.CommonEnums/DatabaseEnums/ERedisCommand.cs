using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

public enum ERedisCommand
{
    [Display(Name = "Get")] Get,
    [Display(Name = "Set")] Set,
    [Display(Name = "Delete")] Delete,
    [Display(Name = "GetMany")] GetMany,
    [Display(Name = "SetMany")] SetMany,
    [Display(Name = "DeleteMany")] DeleteMany, 
    [Display(Name = "")] Customize,
}