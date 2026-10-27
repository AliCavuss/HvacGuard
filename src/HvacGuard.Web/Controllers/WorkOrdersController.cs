using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Models;
using HvacGuard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

public class WorkOrdersController(
    IWorkOrderService workOrders,
    IAlarmService alarms,
    SelectListService selectLists) : Controller
{
    public async Task<IActionResult> Index([FromQuery] WorkOrderQuery query, CancellationToken ct)
    {
        return View(new WorkOrderListViewModel
        {
            Result = await workOrders.SearchAsync(query, ct),
            Query = query,
            VesselOptions = await selectLists.VesselsAsync(query.VesselId, ct),
            AssigneeOptions = await selectLists.AssigneesAsync(query.AssignedTo)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct) => View(await workOrders.GetAsync(id, ct));

    /// <summary>New work order – optionally pre-filled from a unit or from an alarm.</summary>
    [Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> Create(int? unitId, long? alarmId, CancellationToken ct)
    {
        var model = new WorkOrderFormModel { UnitId = unitId ?? 0 };

        if (alarmId.HasValue)
        {
            var alarm = await alarms.GetAsync(alarmId.Value, ct);
            model.UnitId = alarm.UnitId;
            model.AlarmId = alarm.Id;
            model.AlarmMessage = alarm.Message;
            model.Title = $"Investigate: {alarm.Message}";
            if (model.Title.Length > 150) model.Title = model.Title[..150];
            model.Description = $"Raised {alarm.RaisedAt:yyyy-MM-dd HH:mm} UTC on {alarm.Unit.Vessel.Name} / {alarm.Unit.Tag}.";
            model.Priority = alarm.Severity == AlarmSeverity.Critical ? WorkOrderPriority.Urgent : WorkOrderPriority.High;
            model.DueDate = DateTime.UtcNow.Date.AddDays(alarm.Severity == AlarmSeverity.Critical ? 1 : 3);
        }

        await PopulateOptionsAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> Create(WorkOrderFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View("Form", model);
        }

        var workOrder = await workOrders.CreateAsync(model.ToInput(), User.UserName(), ct);
        TempData["Success"] = $"Work order {workOrder.Number} was created.";
        return RedirectToAction(nameof(Details), new { id = workOrder.Id });
    }

    [Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var workOrder = await workOrders.GetAsync(id, ct);
        if (workOrder.IsClosed)
        {
            TempData["Error"] = "Closed work orders cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var model = WorkOrderFormModel.From(workOrder);
        await PopulateOptionsAsync(model, ct);
        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> Edit(int id, WorkOrderFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, ct);
            return View("Form", model);
        }

        await workOrders.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Work order was saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> ChangeStatus(int id, WorkOrderStatus status, string? notes, CancellationToken ct)
    {
        await workOrders.ChangeStatusAsync(id, status, notes, ct);
        TempData["Success"] = $"Work order moved to '{status}'.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateOptionsAsync(WorkOrderFormModel model, CancellationToken ct)
    {
        model.UnitOptions = await selectLists.UnitsAsync(model.UnitId, ct);
        model.AssigneeOptions = await selectLists.AssigneesAsync(model.AssignedTo);
    }
}
