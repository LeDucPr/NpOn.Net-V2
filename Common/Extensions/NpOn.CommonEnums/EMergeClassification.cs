using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

/// <summary>
/// Classification of the outcome after executing a MERGE or Upsert operation.
/// Absolute prohibition of magic numbers (0, 1, 2) in source code.
/// </summary>
public enum EMergeClassification
{
    [Display(Name = "No-Op")]
    NoOp = 0,

    [Display(Name = "Inserted")]
    Inserted = 1,

    [Display(Name = "Updated")]
    Updated = 2,

    [Display(Name = "Deleted")]
    Deleted = 3,

    [Display(Name = "Concurrency Conflict")]
    Conflict = 4
}
