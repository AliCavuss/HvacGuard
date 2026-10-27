using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed record DashboardKpis(
    int VesselCount,
    int UnitCount,
    int OnlineUnitCount,
    int OpenAlarmCount,
    int UnacknowledgedAlarmCount,
    int CriticalAlarmCount,
    int OpenWorkOrderCount,
    int OverdueWorkOrderCount);

public sealed class DashboardSummary
{
    public required DashboardKpis Kpis { get; init; }
    public IReadOnlyList<UnitStatusDto> Units { get; init; } = [];
    public IReadOnlyList<Alarm> RecentAlarms { get; init; } = [];
}

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default);
}

public sealed class DashboardService(
    ApplicationDbContext db,
    IUnitService units,
    IAlarmService alarms,
    TimeProvider clock) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var kpis = await GetKpisAsync(ct);
        var unitStatuses = await units.GetStatusesAsync(null, ct);
        var recent = await alarms.GetRecentAsync(8, ct);

        return new DashboardSummary { Kpis = kpis, Units = unitStatuses, RecentAlarms = recent };
    }

    public async Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var onlineSince = now - UnitStatusDto.OfflineAfter;

        var vesselCount = await db.Vessels.CountAsync(ct);
        var unitCount = await db.Units.CountAsync(u => u.IsActive, ct);
        var onlineCount = await db.Units.CountAsync(u => u.IsActive && u.LastSeenAt >= onlineSince, ct);

        var openAlarms = db.Alarms.Where(a => a.ClearedAt == null || a.AcknowledgedAt == null);
        var openAlarmCount = await openAlarms.CountAsync(ct);
        var unackedCount = await openAlarms.CountAsync(a => a.AcknowledgedAt == null, ct);
        var criticalCount = await db.Alarms.CountAsync(a => a.ClearedAt == null && a.Severity == AlarmSeverity.Critical, ct);

        var openWorkOrders = db.WorkOrders.Where(w => w.Status != WorkOrderStatus.Completed && w.Status != WorkOrderStatus.Cancelled);
        var openWorkOrderCount = await openWorkOrders.CountAsync(ct);
        var overdueCount = await openWorkOrders.CountAsync(w => w.DueDate != null && w.DueDate < now, ct);

        return new DashboardKpis(vesselCount, unitCount, onlineCount, openAlarmCount, unackedCount,
            criticalCount, openWorkOrderCount, overdueCount);
    }
}
