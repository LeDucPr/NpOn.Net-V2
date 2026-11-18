using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

/// <summary>
/// Lifecycle status of a distributed Saga instance or Saga Step.
/// </summary>
public enum ESagaStatus
{
    [Display(Name = "Pending")]
    Pending = 0,

    [Display(Name = "Executing")]
    Executing = 1,

    [Display(Name = "Success")]
    Success = 2,

    [Display(Name = "Compensating")]
    Compensating = 3,

    [Display(Name = "Compensated")]
    Compensated = 4,

    [Display(Name = "Failed")]
    Failed = 5,

    [Display(Name = "Dead Letter")]
    DeadLetter = 6
}
