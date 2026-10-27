using HvacGuard.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

public class DashboardController(IDashboardService dashboard, IUnitService units) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await dashboard.GetSummaryAsync(ct));

    /// <summary>KPI counters + highest active severity per unit, polled by the dashboard after alarm events.</summary>
    [HttpGet]
    public async Task<IActionResult> Kpis(CancellationToken ct)
    {
        var kpis = await dashboard.GetKpisAsync(ct);
        var statuses = await units.GetStatusesAsync(null, ct);

        return Json(new
        {
            kpis,
            units = statuses.Select(s => new
            {
                s.Id,
                severity = s.HighestActiveSeverity?.ToString(),
                s.OpenAlarmCount
            })
        });
    }
}
