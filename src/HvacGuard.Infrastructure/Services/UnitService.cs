using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed record UnitInput(
    int VesselId,
    string Tag,
    string Name,
    string Location,
    string? Manufacturer,
    int RatedAirflow,
    double SupplyTempSetpoint,
    DateTime? CommissionedOn,
    bool IsActive);

public sealed class ReadingDto
{
    public DateTime Timestamp { get; init; }
    public double SupplyAirTemperature { get; init; }
    public double ReturnAirTemperature { get; init; }
    public double RelativeHumidity { get; init; }
    public double FanSpeed { get; init; }
    public double FilterPressureDrop { get; init; }
}

public sealed class UnitStatusDto
{
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(1);

    public int Id { get; init; }
    public string Tag { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public int VesselId { get; init; }
    public string VesselName { get; init; } = string.Empty;
    public double SupplyTempSetpoint { get; init; }
    public bool IsActive { get; init; }
    public DateTime? LastSeenAt { get; init; }
    public ReadingDto? Latest { get; init; }
    public int OpenAlarmCount { get; init; }
    public AlarmSeverity? HighestActiveSeverity { get; init; }

    public bool IsOnline(DateTime now) => LastSeenAt.HasValue && now - LastSeenAt.Value < OfflineAfter;
}

public interface IUnitService
{
    Task<AirHandlingUnit> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UnitStatusDto>> GetStatusesAsync(int? vesselId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AirHandlingUnit>> GetAllAsync(CancellationToken ct = default);
    Task<bool> TagExistsAsync(int vesselId, string tag, int? excludeId, CancellationToken ct = default);
    Task<AirHandlingUnit> CreateAsync(UnitInput input, CancellationToken ct = default);
    Task UpdateAsync(int id, UnitInput input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class UnitService(ApplicationDbContext db) : IUnitService
{
    public async Task<AirHandlingUnit> GetAsync(int id, CancellationToken ct = default) =>
        await db.Units.AsNoTracking().Include(u => u.Vessel).FirstOrDefaultAsync(u => u.Id == id, ct)
        ?? throw new NotFoundException("Air handling unit", id);

    public async Task<IReadOnlyList<UnitStatusDto>> GetStatusesAsync(int? vesselId = null, CancellationToken ct = default)
    {
        var query = db.Units.AsNoTracking();
        if (vesselId.HasValue)
        {
            query = query.Where(u => u.VesselId == vesselId.Value);
        }

        return await query
            .OrderBy(u => u.Vessel.Name).ThenBy(u => u.Tag)
            .Select(u => new UnitStatusDto
            {
                Id = u.Id,
                Tag = u.Tag,
                Name = u.Name,
                Location = u.Location,
                VesselId = u.VesselId,
                VesselName = u.Vessel.Name,
                SupplyTempSetpoint = u.SupplyTempSetpoint,
                IsActive = u.IsActive,
                LastSeenAt = u.LastSeenAt,
                Latest = u.Readings
                    .OrderByDescending(r => r.Timestamp)
                    .Select(r => new ReadingDto
                    {
                        Timestamp = r.Timestamp,
                        SupplyAirTemperature = r.SupplyAirTemperature,
                        ReturnAirTemperature = r.ReturnAirTemperature,
                        RelativeHumidity = r.RelativeHumidity,
                        FanSpeed = r.FanSpeed,
                        FilterPressureDrop = r.FilterPressureDrop
                    })
                    .FirstOrDefault(),
                OpenAlarmCount = u.Alarms.Count(a => a.ClearedAt == null || a.AcknowledgedAt == null),
                HighestActiveSeverity = u.Alarms
                    .Where(a => a.ClearedAt == null)
                    .Max(a => (AlarmSeverity?)a.Severity)
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AirHandlingUnit>> GetAllAsync(CancellationToken ct = default) =>
        await db.Units.AsNoTracking()
            .Include(u => u.Vessel)
            .OrderBy(u => u.Vessel.Name).ThenBy(u => u.Tag)
            .ToListAsync(ct);

    public Task<bool> TagExistsAsync(int vesselId, string tag, int? excludeId, CancellationToken ct = default)
    {
        var normalized = NormalizeTag(tag);
        return db.Units.AnyAsync(
            u => u.VesselId == vesselId && u.Tag == normalized && (excludeId == null || u.Id != excludeId), ct);
    }

    public async Task<AirHandlingUnit> CreateAsync(UnitInput input, CancellationToken ct = default)
    {
        if (!await db.Vessels.AnyAsync(v => v.Id == input.VesselId, ct))
        {
            throw new NotFoundException(nameof(Vessel), input.VesselId);
        }

        var unit = new AirHandlingUnit();
        Apply(unit, input);
        db.Units.Add(unit);
        await db.SaveChangesAsync(ct);
        return unit;
    }

    public async Task UpdateAsync(int id, UnitInput input, CancellationToken ct = default)
    {
        var unit = await db.Units.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Air handling unit", id);
        Apply(unit, input with { VesselId = unit.VesselId });
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var unit = await db.Units.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Air handling unit", id);
        await db.WorkOrders.Where(w => w.UnitId == id).ExecuteDeleteAsync(ct);
        db.Units.Remove(unit);
        await db.SaveChangesAsync(ct);
    }

    private static string NormalizeTag(string tag) => tag.Trim().ToUpperInvariant();

    private static void Apply(AirHandlingUnit unit, UnitInput input)
    {
        unit.VesselId = input.VesselId;
        unit.Tag = NormalizeTag(input.Tag);
        unit.Name = input.Name.Trim();
        unit.Location = input.Location.Trim();
        unit.Manufacturer = string.IsNullOrWhiteSpace(input.Manufacturer) ? null : input.Manufacturer.Trim();
        unit.RatedAirflow = input.RatedAirflow;
        unit.SupplyTempSetpoint = input.SupplyTempSetpoint;
        unit.CommissionedOn = input.CommissionedOn;
        unit.IsActive = input.IsActive;
    }
}
