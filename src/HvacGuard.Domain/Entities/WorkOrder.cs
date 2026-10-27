using HvacGuard.Domain.Common;
using HvacGuard.Domain.Enums;

namespace HvacGuard.Domain.Entities;

/// <summary>
/// A maintenance work order. Status changes go through explicit methods so the workflow
/// (Open → In progress ⇄ On hold → Completed / Cancelled) cannot be bypassed.
/// </summary>
public class WorkOrder
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int UnitId { get; set; }
    public AirHandlingUnit Unit { get; set; } = null!;

    /// <summary>The alarm this work order was created from (optional).</summary>
    public long? AlarmId { get; set; }
    public Alarm? Alarm { get; set; }

    public WorkOrderPriority Priority { get; set; } = WorkOrderPriority.Medium;
    public WorkOrderStatus Status { get; private set; } = WorkOrderStatus.Open;

    public string? AssignedTo { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public string? ResolutionNotes { get; private set; }

    public string Number => $"WO-{Id:D5}";

    public bool IsClosed => Status is WorkOrderStatus.Completed or WorkOrderStatus.Cancelled;

    public bool IsOverdue(DateTime now) => !IsClosed && DueDate.HasValue && DueDate.Value < now;

    public bool CanTransitionTo(WorkOrderStatus target) => (Status, target) switch
    {
        (WorkOrderStatus.Open, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.Open, WorkOrderStatus.Cancelled) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.OnHold) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Completed) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled) => true,
        (WorkOrderStatus.OnHold, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled) => true,
        _ => false
    };

    public IReadOnlyList<WorkOrderStatus> AllowedTransitions() =>
        Enum.GetValues<WorkOrderStatus>().Where(CanTransitionTo).ToList();

    public void Start(DateTime now)
    {
        EnsureCanTransitionTo(WorkOrderStatus.InProgress);
        Status = WorkOrderStatus.InProgress;
        StartedAt ??= now;
    }

    public void PutOnHold()
    {
        EnsureCanTransitionTo(WorkOrderStatus.OnHold);
        Status = WorkOrderStatus.OnHold;
    }

    public void Complete(string? resolutionNotes, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(resolutionNotes))
        {
            throw new DomainException("Resolution notes are required to complete a work order.");
        }

        EnsureCanTransitionTo(WorkOrderStatus.Completed);
        Status = WorkOrderStatus.Completed;
        ResolutionNotes = resolutionNotes.Trim();
        ClosedAt = now;
    }

    public void Cancel(string? reason, DateTime now)
    {
        EnsureCanTransitionTo(WorkOrderStatus.Cancelled);
        Status = WorkOrderStatus.Cancelled;
        ResolutionNotes = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        ClosedAt = now;
    }

    private void EnsureCanTransitionTo(WorkOrderStatus target)
    {
        if (!CanTransitionTo(target))
        {
            throw new DomainException($"A work order cannot move from '{Status}' to '{target}'.");
        }
    }
}
