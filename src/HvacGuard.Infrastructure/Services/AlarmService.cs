using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Common;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed class AlarmHistoryQuery
{
    public int? VesselId { get; set; }
    public int? UnitId { get; set; }
    public AlarmSeverity? Severity { get; set; }
    public SensorParameter? Parameter { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public interface IAlarmService
{
    Task<Alarm> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> GetOpenAlarmsAsync(int? vesselId = null, int? unitId = null, AlarmSeverity? severity = null, CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> GetRecentAsync(int count, CancellationToken ct = default);
    Task<PagedResult<Alarm>> SearchHistoryAsync(AlarmHistoryQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> ExportHistoryAsync(AlarmHistoryQuery query, int maxRows = 10_000, CancellationToken ct = default);
    Task<Alarm> AcknowledgeAsync(long id, string user, CancellationToken ct = default);
    Task<IReadOnlyList<Alarm>> AcknowledgeAllAsync(string user, int? unitId = null, CancellationToken ct = default);
}

public sealed class AlarmService(ApplicationDbContext db, TimeProvider clock) : IAlarmService
{
    private IQueryable<Alarm> AlarmsWithUnit() =>
        db.Alarms.AsNoTracking().Include(a => a.Unit).ThenInclude(u => u.Vessel);

    public async Task<Alarm> GetAsync(long id, CancellationToken ct = default) =>
        await AlarmsWithUnit().FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException(nameof(Alarm), id);

    public async Task<IReadOnlyList<Alarm>> GetOpenAlarmsAsync(
        int? vesselId = null, int? unitId = null, AlarmSeverity? severity = null, CancellationToken ct = default)
    {
        var query = AlarmsWithUnit().Where(a => a.ClearedAt == null || a.AcknowledgedAt == null);

        if (vesselId.HasValue) query = query.Where(a => a.Unit.VesselId == vesselId.Value);
        if (unitId.HasValue) query = query.Where(a => a.UnitId == unitId.Value);
        if (severity.HasValue) query = query.Where(a => a.Severity == severity.Value);

        // Unacknowledged first, then most severe, then newest – the order an operator works the list in.
        return await query
            .OrderBy(a => a.AcknowledgedAt != null)
            .ThenByDescending(a => a.Severity)
            .ThenByDescending(a => a.RaisedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Alarm>> GetRecentAsync(int count, CancellationToken ct = default) =>
        await AlarmsWithUnit().OrderByDescending(a => a.RaisedAt).Take(count).ToListAsync(ct);

    public Task<PagedResult<Alarm>> SearchHistoryAsync(AlarmHistoryQuery query, CancellationToken ct = default) =>
        PagedResult<Alarm>.CreateAsync(ApplyFilters(query), query.Page, query.PageSize, ct);

    public async Task<IReadOnlyList<Alarm>> ExportHistoryAsync(
        AlarmHistoryQuery query, int maxRows = 10_000, CancellationToken ct = default) =>
        await ApplyFilters(query).Take(maxRows).ToListAsync(ct);

    public async Task<Alarm> AcknowledgeAsync(long id, string user, CancellationToken ct = default)
    {
        var alarm = await db.Alarms.Include(a => a.Unit).ThenInclude(u => u.Vessel)
                        .FirstOrDefaultAsync(a => a.Id == id, ct)
                    ?? throw new NotFoundException(nameof(Alarm), id);

        alarm.Acknowledge(user, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return alarm;
    }

    public async Task<IReadOnlyList<Alarm>> AcknowledgeAllAsync(string user, int? unitId = null, CancellationToken ct = default)
    {
        var query = db.Alarms.Where(a => a.AcknowledgedAt == null);
        if (unitId.HasValue)
        {
            query = query.Where(a => a.UnitId == unitId.Value);
        }

        var alarms = await query.ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var alarm in alarms)
        {
            alarm.Acknowledge(user, now);
        }

        await db.SaveChangesAsync(ct);
        return alarms;
    }

    private IQueryable<Alarm> ApplyFilters(AlarmHistoryQuery q)
    {
        var query = AlarmsWithUnit();

        if (q.VesselId.HasValue) query = query.Where(a => a.Unit.VesselId == q.VesselId.Value);
        if (q.UnitId.HasValue) query = query.Where(a => a.UnitId == q.UnitId.Value);
        if (q.Severity.HasValue) query = query.Where(a => a.Severity == q.Severity.Value);
        if (q.Parameter.HasValue) query = query.Where(a => a.Parameter == q.Parameter.Value);
        if (q.From.HasValue) query = query.Where(a => a.RaisedAt >= q.From.Value);
        if (q.To.HasValue)
        {
            var toExclusive = q.To.Value.Date.AddDays(1);
            query = query.Where(a => a.RaisedAt < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim();
            query = query.Where(a => a.Message.Contains(term) || a.Unit.Tag.Contains(term) || a.Unit.Vessel.Name.Contains(term));
        }

        return query.OrderByDescending(a => a.RaisedAt);
    }
}
