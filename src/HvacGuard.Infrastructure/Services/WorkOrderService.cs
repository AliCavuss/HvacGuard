using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Common;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed record WorkOrderInput(
    string Title,
    string? Description,
    int UnitId,
    long? AlarmId,
    WorkOrderPriority Priority,
    string? AssignedTo,
    DateTime? DueDate);

public sealed class WorkOrderQuery
{
    public WorkOrderStatus? Status { get; set; }
    public WorkOrderPriority? Priority { get; set; }
    public int? VesselId { get; set; }
    public int? UnitId { get; set; }
    public string? AssignedTo { get; set; }
    public string? Search { get; set; }

    /// <summary>When true (default) only work orders that are not completed / cancelled are listed.</summary>
    public bool OpenOnly { get; set; } = true;

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public interface IWorkOrderService
{
    Task<PagedResult<WorkOrder>> SearchAsync(WorkOrderQuery query, CancellationToken ct = default);
    Task<WorkOrder> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkOrder>> GetForUnitAsync(int unitId, int take = 10, CancellationToken ct = default);
    Task<WorkOrder> CreateAsync(WorkOrderInput input, string createdBy, CancellationToken ct = default);
    Task UpdateAsync(int id, WorkOrderInput input, CancellationToken ct = default);
    Task ChangeStatusAsync(int id, WorkOrderStatus target, string? notes, CancellationToken ct = default);
}

public sealed class WorkOrderService(ApplicationDbContext db, TimeProvider clock) : IWorkOrderService
{
    private IQueryable<WorkOrder> WorkOrdersWithUnit() =>
        db.WorkOrders.AsNoTracking().Include(w => w.Unit).ThenInclude(u => u.Vessel);

    public Task<PagedResult<WorkOrder>> SearchAsync(WorkOrderQuery q, CancellationToken ct = default)
    {
        var query = WorkOrdersWithUnit();

        if (q.Status.HasValue)
        {
            query = query.Where(w => w.Status == q.Status.Value);
        }
        else if (q.OpenOnly)
        {
            query = query.Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled);
        }

        if (q.Priority.HasValue) query = query.Where(w => w.Priority == q.Priority.Value);
        if (q.VesselId.HasValue) query = query.Where(w => w.Unit.VesselId == q.VesselId.Value);
        if (q.UnitId.HasValue) query = query.Where(w => w.UnitId == q.UnitId.Value);
        if (!string.IsNullOrWhiteSpace(q.AssignedTo)) query = query.Where(w => w.AssignedTo == q.AssignedTo);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(w => w.Title.Contains(term)
                                     || (w.Description != null && w.Description.Contains(term))
                                     || w.Unit.Tag.Contains(term));
        }

        query = query
            .OrderByDescending(w => w.Priority)
            .ThenBy(w => w.DueDate == null)
            .ThenBy(w => w.DueDate)
            .ThenByDescending(w => w.CreatedAt);

        return PagedResult<WorkOrder>.CreateAsync(query, q.Page, q.PageSize, ct);
    }

    public async Task<WorkOrder> GetAsync(int id, CancellationToken ct = default) =>
        await WorkOrdersWithUnit().Include(w => w.Alarm).FirstOrDefaultAsync(w => w.Id == id, ct)
        ?? throw new NotFoundException("Work order", id);

    public async Task<IReadOnlyList<WorkOrder>> GetForUnitAsync(int unitId, int take = 10, CancellationToken ct = default) =>
        await db.WorkOrders.AsNoTracking()
            .Where(w => w.UnitId == unitId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<WorkOrder> CreateAsync(WorkOrderInput input, string createdBy, CancellationToken ct = default)
    {
        await ValidateAsync(input, ct);

        var workOrder = new WorkOrder
        {
            CreatedBy = createdBy,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        Apply(workOrder, input);

        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(ct);
        return workOrder;
    }

    public async Task UpdateAsync(int id, WorkOrderInput input, CancellationToken ct = default)
    {
        var workOrder = await db.WorkOrders.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Work order", id);
        if (workOrder.IsClosed)
        {
            throw new DomainException("Closed work orders cannot be edited.");
        }

        await ValidateAsync(input, ct);
        Apply(workOrder, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(int id, WorkOrderStatus target, string? notes, CancellationToken ct = default)
    {
        var workOrder = await db.WorkOrders.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Work order", id);
        var now = clock.GetUtcNow().UtcDateTime;

        switch (target)
        {
            case WorkOrderStatus.InProgress:
                workOrder.Start(now);
                break;
            case WorkOrderStatus.OnHold:
                workOrder.PutOnHold();
                break;
            case WorkOrderStatus.Completed:
                workOrder.Complete(notes, now);
                break;
            case WorkOrderStatus.Cancelled:
                workOrder.Cancel(notes, now);
                break;
            default:
                throw new DomainException($"Cannot change a work order to '{target}'.");
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateAsync(WorkOrderInput input, CancellationToken ct)
    {
        if (!await db.Units.AnyAsync(u => u.Id == input.UnitId, ct))
        {
            throw new DomainException("The selected air handling unit does not exist.");
        }

        if (input.AlarmId.HasValue
            && !await db.Alarms.AnyAsync(a => a.Id == input.AlarmId.Value && a.UnitId == input.UnitId, ct))
        {
            throw new DomainException("The linked alarm does not belong to the selected unit.");
        }
    }

    private static void Apply(WorkOrder workOrder, WorkOrderInput input)
    {
        workOrder.Title = input.Title.Trim();
        workOrder.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        workOrder.UnitId = input.UnitId;
        workOrder.AlarmId = input.AlarmId;
        workOrder.Priority = input.Priority;
        workOrder.AssignedTo = string.IsNullOrWhiteSpace(input.AssignedTo) ? null : input.AssignedTo.Trim();
        workOrder.DueDate = input.DueDate;
    }
}
