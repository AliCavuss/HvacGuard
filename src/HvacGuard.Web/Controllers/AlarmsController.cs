using System.Globalization;
using System.Text;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Helpers;
using HvacGuard.Web.Models;
using HvacGuard.Web.Services;
using HvacGuard.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers;

public class AlarmsController(
    IAlarmService alarms,
    IRealtimeNotifier notifier,
    SelectListService selectLists) : Controller
{
    /// <summary>Active alarm list (ISA-18.2 style: active or not yet acknowledged).</summary>
    public async Task<IActionResult> Index(int? vesselId, AlarmSeverity? severity, CancellationToken ct)
    {
        return View(new AlarmListViewModel
        {
            Alarms = await alarms.GetOpenAlarmsAsync(vesselId, null, severity, ct),
            VesselId = vesselId,
            Severity = severity,
            VesselOptions = await selectLists.VesselsAsync(vesselId, ct)
        });
    }

    public async Task<IActionResult> History([FromQuery] AlarmHistoryQuery query, string? format, CancellationToken ct)
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return await ExportCsv(query, ct);
        }

        return View(new AlarmHistoryViewModel
        {
            Result = await alarms.SearchHistoryAsync(query, ct),
            Query = query,
            VesselOptions = await selectLists.VesselsAsync(query.VesselId, ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> Acknowledge(long id, string? returnUrl, CancellationToken ct)
    {
        var user = User.UserName();
        var alarm = await alarms.AcknowledgeAsync(id, user, ct);
        await notifier.PublishAcknowledgedAsync([alarm], user, ct);

        TempData["Success"] = $"Alarm on {alarm.Unit.Tag} acknowledged.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = Policies.CanOperate)]
    public async Task<IActionResult> AcknowledgeAll(int? unitId, string? returnUrl, CancellationToken ct)
    {
        var user = User.UserName();
        var acknowledged = await alarms.AcknowledgeAllAsync(user, unitId, ct);
        await notifier.PublishAcknowledgedAsync(acknowledged, user, ct);

        TempData["Success"] = acknowledged.Count == 0
            ? "There were no unacknowledged alarms."
            : $"{acknowledged.Count} alarm(s) acknowledged.";
        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    private async Task<IActionResult> ExportCsv(AlarmHistoryQuery query, CancellationToken ct)
    {
        var rows = await alarms.ExportHistoryAsync(query, ct: ct);
        var csv = new StringBuilder();
        csv.AppendLine("Id,Vessel,Unit,Parameter,Severity,Message,TriggerValue,Threshold,RaisedAtUtc,AcknowledgedAtUtc,AcknowledgedBy,ClearedAtUtc");

        foreach (var a in rows)
        {
            csv.AppendLine(string.Join(',',
                a.Id.ToString(CultureInfo.InvariantCulture),
                Escape(a.Unit.Vessel.Name),
                Escape(a.Unit.Tag),
                a.Parameter.ToString(),
                a.Severity.ToString(),
                Escape(a.Message),
                a.TriggerValue.ToString("0.##", CultureInfo.InvariantCulture),
                a.Threshold.ToString("0.##", CultureInfo.InvariantCulture),
                Iso(a.RaisedAt),
                Iso(a.AcknowledgedAt),
                Escape(a.AcknowledgedBy),
                Iso(a.ClearedAt)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"alarm-history-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");

        static string Iso(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

        static string Escape(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.IndexOfAny([',', '"', '\n', '\r']) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
