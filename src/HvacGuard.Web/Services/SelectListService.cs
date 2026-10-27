using HvacGuard.Domain.Constants;
using HvacGuard.Infrastructure.Identity;
using HvacGuard.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HvacGuard.Web.Services;

/// <summary>Builds drop-down options used by several forms and filters.</summary>
public sealed class SelectListService(
    IVesselService vessels,
    IUnitService units,
    UserManager<ApplicationUser> userManager)
{
    public async Task<IReadOnlyList<SelectListItem>> VesselsAsync(int? selected = null, CancellationToken ct = default)
    {
        var list = await vessels.GetListAsync(ct);
        return list.Select(v => new SelectListItem(v.Name, v.Id.ToString(), v.Id == selected)).ToList();
    }

    public async Task<IReadOnlyList<SelectListItem>> UnitsAsync(int? selected = null, CancellationToken ct = default)
    {
        var list = await units.GetAllAsync(ct);
        return list
            .Select(u => new SelectListItem($"{u.Vessel.Name} · {u.Tag} – {u.Name}", u.Id.ToString(), u.Id == selected))
            .ToList();
    }

    /// <summary>Users who can be assigned work orders (admins and engineers).</summary>
    public async Task<IReadOnlyList<SelectListItem>> AssigneesAsync(string? selected = null)
    {
        var engineers = await userManager.GetUsersInRoleAsync(Roles.Engineer);
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);

        return engineers.Concat(admins)
            .DistinctBy(u => u.Id)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem($"{u.FullName} ({u.Email})", u.UserName, u.UserName == selected))
            .ToList();
    }
}
