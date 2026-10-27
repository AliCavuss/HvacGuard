using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

[Authorize(Policy = Policies.CanManageAssets)]
public class AlarmRulesController(IAlarmRuleService rules) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await rules.GetAllAsync(ct));

    public IActionResult Create() => View("Form", new AlarmRuleFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AlarmRuleFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        await rules.CreateAsync(model.ToInput(), ct);
        TempData["Success"] = $"Alarm rule '{model.Name}' was created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct) =>
        View("Form", AlarmRuleFormModel.From(await rules.GetAsync(id, ct)));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AlarmRuleFormModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", model);
        }

        await rules.UpdateAsync(id, model.ToInput(), ct);
        TempData["Success"] = "Alarm rule was saved. It applies to the next incoming readings.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var enabled = await rules.ToggleAsync(id, ct);
        TempData["Success"] = enabled ? "Rule enabled." : "Rule disabled – its active alarms will clear on the next reading.";
        return RedirectToAction(nameof(Index));
    }
}
