using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums.DatabaseEnums;

public enum EExecType
{
    [Display(Name = "Query")] Query = 1,
    [Display(Name = "Func")] ExecFunc = 2, 
}