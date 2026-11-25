using System.ComponentModel.DataAnnotations;

namespace Common.Extensions.NpOn.Saga;

/// <summary>
/// Domain entity types participating in distributed Saga workflows.
/// Stored as 2-byte smallint (INT2) to achieve ultra-compact 10-byte compound keys.
/// Granular Incremental Build verification marker.
/// </summary>
public enum ESagaEntityType : short
{
    [Display(Name = "Unknown")]
    Unknown = 0,

    [Display(Name = "Account")]
    Account = 1,

    [Display(Name = "Transaction")]
    Transaction = 2,

    [Display(Name = "Device")]
    Device = 3,

    [Display(Name = "Telemetry")]
    Telemetry = 4,

    [Display(Name = "Patient")]
    Patient = 5,

    [Display(Name = "Billing")]
    Billing = 6,

    [Display(Name = "Order")]
    Order = 7
}

