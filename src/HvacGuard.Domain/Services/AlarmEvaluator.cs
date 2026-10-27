using HvacGuard.Domain.Entities;

namespace HvacGuard.Domain.Services;

public sealed record AlarmEvaluationResult(IReadOnlyList<Alarm> Raised, IReadOnlyList<Alarm> Cleared);

/// <summary>
/// Pure alarm logic (no database, no clock) so it is fully unit-testable:
/// <list type="bullet">
///   <item>raises a new alarm when an enabled rule is violated and no active alarm exists for that rule;</item>
///   <item>clears active alarms when the value has returned to normal (respecting the deadband)
///         or when the rule was disabled/deleted.</item>
/// </list>
/// </summary>
public static class AlarmEvaluator
{
    public static AlarmEvaluationResult Evaluate(
        SensorReading reading,
        IEnumerable<AlarmRule> rules,
        IEnumerable<Alarm> activeAlarms,
        DateTime now)
    {
        var ruleList = rules.ToList();
        var active = activeAlarms.Where(a => a.IsActive && a.UnitId == reading.UnitId).ToList();

        var cleared = new List<Alarm>();
        foreach (var alarm in active)
        {
            var rule = ruleList.FirstOrDefault(r => r.Id == alarm.AlarmRuleId);
            var value = reading.GetValue(alarm.Parameter);

            if (rule is null || !rule.IsEnabled || rule.HasReturnedToNormal(value))
            {
                alarm.Clear(now);
                cleared.Add(alarm);
            }
        }

        var raised = new List<Alarm>();
        foreach (var rule in ruleList.Where(r => r.IsEnabled))
        {
            var value = reading.GetValue(rule.Parameter);
            if (!rule.IsViolatedBy(value))
            {
                continue;
            }

            var alreadyActive = active.Any(a => a.AlarmRuleId == rule.Id && a.IsActive);
            if (!alreadyActive)
            {
                raised.Add(Alarm.Raise(reading.UnitId, rule, value, now));
            }
        }

        return new AlarmEvaluationResult(raised, cleared);
    }
}
