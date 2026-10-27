using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Services;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HvacGuard.Infrastructure.Services;

public sealed record TelemetryInput(
    int UnitId,
    DateTime? Timestamp,
    double SupplyAirTemperature,
    double ReturnAirTemperature,
    double RelativeHumidity,
    double FanSpeed,
    double FilterPressureDrop);

public sealed record TelemetryIngestResult(
    SensorReading Reading,
    string UnitTag,
    string VesselName,
    IReadOnlyList<Alarm> RaisedAlarms,
    IReadOnlyList<Alarm> ClearedAlarms);

public interface ITelemetryService
{
    /// <summary>Stores a reading, evaluates the alarm rules and raises / clears alarms.</summary>
    Task<TelemetryIngestResult> IngestAsync(TelemetryInput input, CancellationToken ct = default);

    Task<IReadOnlyList<ReadingDto>> GetReadingsAsync(int unitId, DateTime sinceUtc, CancellationToken ct = default);

    /// <summary>Deletes readings older than <paramref name="olderThanUtc"/>. Returns the number of deleted rows.</summary>
    Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct = default);
}

public sealed class TelemetryService(
    ApplicationDbContext db,
    TimeProvider clock,
    ILogger<TelemetryService> logger) : ITelemetryService
{
    public async Task<TelemetryIngestResult> IngestAsync(TelemetryInput input, CancellationToken ct = default)
    {
        var unit = await db.Units.Include(u => u.Vessel).FirstOrDefaultAsync(u => u.Id == input.UnitId, ct)
                   ?? throw new NotFoundException("Air handling unit", input.UnitId);

        var now = clock.GetUtcNow().UtcDateTime;
        var reading = new SensorReading
        {
            UnitId = unit.Id,
            Timestamp = input.Timestamp?.ToUniversalTime() ?? now,
            SupplyAirTemperature = input.SupplyAirTemperature,
            ReturnAirTemperature = input.ReturnAirTemperature,
            RelativeHumidity = input.RelativeHumidity,
            FanSpeed = input.FanSpeed,
            FilterPressureDrop = input.FilterPressureDrop
        };

        db.SensorReadings.Add(reading);
        unit.LastSeenAt = reading.Timestamp;

        // Inactive units still store data but do not raise alarms.
        IReadOnlyList<Alarm> raised = [];
        IReadOnlyList<Alarm> cleared = [];
        if (unit.IsActive)
        {
            var rules = await db.AlarmRules.AsNoTracking().ToListAsync(ct);
            var activeAlarms = await db.Alarms
                .Where(a => a.UnitId == unit.Id && a.ClearedAt == null)
                .ToListAsync(ct);

            var evaluation = AlarmEvaluator.Evaluate(reading, rules, activeAlarms, now);
            db.Alarms.AddRange(evaluation.Raised);
            raised = evaluation.Raised;
            cleared = evaluation.Cleared;
        }

        await db.SaveChangesAsync(ct);

        foreach (var alarm in raised)
        {
            logger.LogWarning("Alarm raised on {Vessel}/{Unit}: {Message} ({Severity})",
                unit.Vessel.Name, unit.Tag, alarm.Message, alarm.Severity);
        }

        return new TelemetryIngestResult(reading, unit.Tag, unit.Vessel.Name, raised, cleared);
    }

    public async Task<IReadOnlyList<ReadingDto>> GetReadingsAsync(int unitId, DateTime sinceUtc, CancellationToken ct = default)
    {
        if (!await db.Units.AnyAsync(u => u.Id == unitId, ct))
        {
            throw new NotFoundException("Air handling unit", unitId);
        }

        return await db.SensorReadings
            .AsNoTracking()
            .Where(r => r.UnitId == unitId && r.Timestamp >= sinceUtc)
            .OrderBy(r => r.Timestamp)
            .Take(5000)
            .Select(r => new ReadingDto
            {
                Timestamp = r.Timestamp,
                SupplyAirTemperature = r.SupplyAirTemperature,
                ReturnAirTemperature = r.ReturnAirTemperature,
                RelativeHumidity = r.RelativeHumidity,
                FanSpeed = r.FanSpeed,
                FilterPressureDrop = r.FilterPressureDrop
            })
            .ToListAsync(ct);
    }

    public Task<int> PurgeAsync(DateTime olderThanUtc, CancellationToken ct = default) =>
        db.SensorReadings.Where(r => r.Timestamp < olderThanUtc).ExecuteDeleteAsync(ct);
}
