using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed record VesselInput(string Name, string ImoNumber, string VesselType, string Flag, string? Owner);

public sealed class VesselListItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ImoNumber { get; init; } = string.Empty;
    public string VesselType { get; init; } = string.Empty;
    public string Flag { get; init; } = string.Empty;
    public string? Owner { get; init; }
    public int UnitCount { get; init; }
    public int OpenAlarmCount { get; init; }
    public int OpenWorkOrderCount { get; init; }
}

public interface IVesselService
{
    Task<IReadOnlyList<VesselListItem>> GetListAsync(CancellationToken ct = default);
    Task<Vessel> GetAsync(int id, CancellationToken ct = default);
    Task<bool> ImoExistsAsync(string imoNumber, int? excludeId, CancellationToken ct = default);
    Task<Vessel> CreateAsync(VesselInput input, CancellationToken ct = default);
    Task UpdateAsync(int id, VesselInput input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class VesselService(ApplicationDbContext db, TimeProvider clock) : IVesselService
{
    public async Task<IReadOnlyList<VesselListItem>> GetListAsync(CancellationToken ct = default) =>
        await db.Vessels
            .AsNoTracking()
            .OrderBy(v => v.Name)
            .Select(v => new VesselListItem
            {
                Id = v.Id,
                Name = v.Name,
                ImoNumber = v.ImoNumber,
                VesselType = v.VesselType,
                Flag = v.Flag,
                Owner = v.Owner,
                UnitCount = v.Units.Count,
                OpenAlarmCount = v.Units.SelectMany(u => u.Alarms)
                    .Count(a => a.ClearedAt == null || a.AcknowledgedAt == null),
                OpenWorkOrderCount = v.Units.SelectMany(u => u.WorkOrders)
                    .Count(w => w.Status != WorkOrderStatus.Completed
                                && w.Status != WorkOrderStatus.Cancelled)
            })
            .ToListAsync(ct);

    public async Task<Vessel> GetAsync(int id, CancellationToken ct = default) =>
        await db.Vessels.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct)
        ?? throw new NotFoundException(nameof(Vessel), id);

    public Task<bool> ImoExistsAsync(string imoNumber, int? excludeId, CancellationToken ct = default)
    {
        var normalized = ImoNumberValidator.Normalize(imoNumber);
        return db.Vessels.AnyAsync(v => v.ImoNumber == normalized && (excludeId == null || v.Id != excludeId), ct);
    }

    public async Task<Vessel> CreateAsync(VesselInput input, CancellationToken ct = default)
    {
        var vessel = new Vessel { CreatedAt = clock.GetUtcNow().UtcDateTime };
        Apply(vessel, input);
        db.Vessels.Add(vessel);
        await db.SaveChangesAsync(ct);
        return vessel;
    }

    public async Task UpdateAsync(int id, VesselInput input, CancellationToken ct = default)
    {
        var vessel = await db.Vessels.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException(nameof(Vessel), id);
        Apply(vessel, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var vessel = await db.Vessels.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException(nameof(Vessel), id);

        // Work orders reference alarms with NO ACTION, so remove them first; the rest cascades.
        await db.WorkOrders.Where(w => w.Unit.VesselId == id).ExecuteDeleteAsync(ct);
        db.Vessels.Remove(vessel);
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Vessel vessel, VesselInput input)
    {
        vessel.Name = input.Name.Trim();
        vessel.ImoNumber = ImoNumberValidator.Normalize(input.ImoNumber);
        vessel.VesselType = input.VesselType.Trim();
        vessel.Flag = input.Flag.Trim();
        vessel.Owner = string.IsNullOrWhiteSpace(input.Owner) ? null : input.Owner.Trim();
    }
}
