using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

public class UnitsController(
    IUnitService units,
    IVesselService vessels,
    IAlarmService alarms,
    IAlarmRuleService rules,
    IWorkOrderService workOrders) : Controller
{
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var unit = await units.GetAsync(id, ct);
        var status = (await units.GetStatusesAsync(unit.VesselId, ct)).First(s => s.Id == id);

        return View(new UnitDetailsViewModel
        {
            Unit = unit,
            Status = status,
            OpenAlarms = await alarms.GetOpenAlarmsAsync(unitId: id, ct: ct),
            WorkOrders = await workOrders.GetForUnitAsync(id, 10, ct),
            Rules = (await rules.GetAllAsync(ct)).Where(r => r.IsEnabled).ToList()
        });
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Create(int vesselId, CancellationToken ct)
    {
        var vessel = await vessels.GetAsync(vesselId, ct);
        return View("Form", new UnitFormModel { VesselId = vessel.Id, VesselName = vessel.Name });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Create(UnitFormModel model, CancellationToken ct)
    {
        if (ModelState.IsValid && await units.TagExistsAsync(model.VesselId, model.Tag, null, ct))
        {
            ModelState.AddModelError(nameof(model.Tag), "This tag is already used on this vessel.");
        }

        if (!ModelState.IsValid)
        {
            model.VesselName = (await vessels.GetAsync(model.VesselId, ct)).Name;
            return View("Form", model);
        }

        var unit = await units.CreateAsync(model.ToInput(), ct);
        TempData["Success"] = $"Unit {unit.Tag} was added.";
        return RedirectToAction(nameof(Details), new { id = unit.Id });
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct) =>
        View("Form", UnitFormModel.From(await units.GetAsync(id, ct)));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Edit(int id, UnitFormModel model, CancellationToken ct)
    {
        var existing = await units.GetAsync(id, ct);
        model.Id = id;
        model.VesselId = existing.VesselId;
        model.VesselName = existing.Vessel.Name;

        if (ModelState.IsValid && await units.TagExistsAsync(existing.VesselId, model.Tag, id, ct))
        {
            ModelState.AddModelError(nameof(model.Tag), "This tag is already used on this vessel.");
        }

        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        await units.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Unit details were saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) => View(await units.GetAsync(id, ct));

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Policy = Policies.CanManageAssets)]
    public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
    {
        var unit = await units.GetAsync(id, ct);
        await units.DeleteAsync(id, ct);
        TempData["Success"] = $"Unit {unit.Tag} and its history were deleted.";
        return RedirectToAction("Details", "Vessels", new { id = unit.VesselId });
    }
}
