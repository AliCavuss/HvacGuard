using System.ComponentModel.DataAnnotations;

namespace HvacGuard.Domain.Enums;

public enum AlarmSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public enum ThresholdCondition
{
    [Display(Name = "Above (>)")]
    Above = 1,

    [Display(Name = "Below (<)")]
    Below = 2
}

/// <summary>
/// Alarm states following the ISA-18.2 alarm management life cycle.
/// An alarm is only removed from the active list when it has both returned to normal AND been acknowledged.
/// </summary>
public enum AlarmState
{
    [Display(Name = "Active – unacknowledged")]
    ActiveUnacknowledged = 1,

    [Display(Name = "Active – acknowledged")]
    ActiveAcknowledged = 2,

    [Display(Name = "Cleared – unacknowledged")]
    ClearedUnacknowledged = 3,

    [Display(Name = "Closed")]
    Closed = 4
}
