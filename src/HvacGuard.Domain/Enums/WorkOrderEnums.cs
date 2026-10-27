using System.ComponentModel.DataAnnotations;

namespace HvacGuard.Domain.Enums;

public enum WorkOrderStatus
{
    Open = 1,

    [Display(Name = "In progress")]
    InProgress = 2,

    [Display(Name = "On hold")]
    OnHold = 3,

    Completed = 4,
    Cancelled = 5
}

public enum WorkOrderPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}
