using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;

namespace HvacGuard.Tests;

public class WorkOrderTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static WorkOrder NewWorkOrder() => new() { Id = 42, Title = "Replace filters", UnitId = 1, CreatedAt = Now };

    [Fact]
    public void New_work_order_is_open_with_formatted_number()
    {
        var wo = NewWorkOrder();

        Assert.Equal(WorkOrderStatus.Open, wo.Status);
        Assert.Equal("WO-00042", wo.Number);
        Assert.Equal(new[] { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled }, wo.AllowedTransitions().ToArray());
    }

    [Fact]
    public void Happy_path_open_in_progress_on_hold_completed()
    {
        var wo = NewWorkOrder();

        wo.Start(Now.AddHours(1));
        wo.PutOnHold();
        wo.Start(Now.AddHours(5)); // resume keeps the original start time
        wo.Complete("Filters replaced, ΔP back to 120 Pa.", Now.AddHours(6));

        Assert.Equal(WorkOrderStatus.Completed, wo.Status);
        Assert.Equal(Now.AddHours(1), wo.StartedAt);
        Assert.Equal(Now.AddHours(6), wo.ClosedAt);
        Assert.True(wo.IsClosed);
        Assert.Empty(wo.AllowedTransitions());
    }

    [Fact]
    public void Cannot_complete_without_starting()
    {
        var wo = NewWorkOrder();

        Assert.Throws<DomainException>(() => wo.Complete("done", Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Completing_requires_resolution_notes(string? notes)
    {
        var wo = NewWorkOrder();
        wo.Start(Now);

        Assert.Throws<DomainException>(() => wo.Complete(notes, Now));
        Assert.Equal(WorkOrderStatus.InProgress, wo.Status);
    }

    [Fact]
    public void Closed_work_order_cannot_be_reopened()
    {
        var wo = NewWorkOrder();
        wo.Cancel("Duplicate", Now);

        Assert.Throws<DomainException>(() => wo.Start(Now));
    }

    [Fact]
    public void Overdue_only_when_open_and_past_due()
    {
        var wo = NewWorkOrder();
        wo.DueDate = Now.AddDays(-1);
        Assert.True(wo.IsOverdue(Now));

        wo.Start(Now);
        wo.Complete("done", Now);
        Assert.False(wo.IsOverdue(Now));
    }
}
