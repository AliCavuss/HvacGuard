using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

public class VesselsController(IVesselService vessels, IUnitService units) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await vessels.GetListAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var vessel = await vessels.GetAsync(id, ct);
        var statuses = await units.GetStatusesAsync(id, ct);
        return View(new VesselDetailsViewModel(vessel, statuses));
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public IActionResult Create() => View("Form", new VesselFormModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Create(VesselFormModel model, CancellationToken ct)
    {
        if (ModelState.IsValid && await vessels.ImoExistsAsync(model.ImoNumber, null, ct))
        {
            ModelState.AddModelError(nameof(model.ImoNumber), "A vessel with this IMO number already exists.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        var vessel = await vessels.CreateAsync(model.ToInput(), ct);
        TempData["Success"] = $"Vessel '{vessel.Name}' was created.";
        return RedirectToAction(nameof(Details), new { id = vessel.Id });
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) =>
        View("Form", VesselFormModel.From(await vessels.GetAsync(id, ct)));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Edit(int id, VesselFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (ModelState.IsValid && await vessels.ImoExistsAsync(model.ImoNumber, id, ct))
        {
            ModelState.AddModelError(nameof(model.ImoNumber), "Another vessel already uses this IMO number.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        await vessels.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Vessel details were saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => View(await vessels.GetAsync(id, ct));

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        await vessels.DeleteAsync(id, ct);
        TempData["Success"] = "Vessel and all related data were deleted.";
        return RedirectToAction(nameof(Index));
    }
}
