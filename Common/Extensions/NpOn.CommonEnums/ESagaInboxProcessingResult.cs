using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.CommonEnums;

/// <summary>
/// Result of event processing inside the Transactional Inbox.
/// </summary>
public enum ESagaInboxProcessingResult
{
    [Display(Name = "Unknown")]
    Unknown = 0,

    [Display(Name = "Applied")]
    Applied = 1,

    [Display(Name = "Duplicate Dropped")]
    DuplicateDropped = 2,

    [Display(Name = "Deferred Out-Of-Order")]
    DeferredOutOfOrder = 3,

    [Display(Name = "Conflict Rejected")]
    ConflictRejected = 4,

    [Display(Name = "Dead Letter")]
    DeadLetter = 5
}
