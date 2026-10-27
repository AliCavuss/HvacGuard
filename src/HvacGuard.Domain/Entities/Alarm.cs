using HvacGuard.Domain.Common;
using HvacGuard.Domain.Enums;

namespace HvacGuard.Domain.Entities;

public class Alarm
{
    public long Id { get; set; }

    public int UnitId { get; set; }
    public AirHandlingUnit Unit { get; set; } = null!;

    public int? AlarmRuleId { get; set; }
    public AlarmRule? Rule { get; set; }

    public SensorParameter Parameter { get; set; }
    public AlarmSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>The measured value that triggered the alarm.</summary>
    public double TriggerValue { get; set; }

    public double Threshold { get; set; }

    public DateTime RaisedAt { get; set; }

    /// <summary>When the process value returned to normal. <c>null</c> while the condition is still present.</summary>
    public DateTime? ClearedAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }

    public bool IsActive => ClearedAt is null;
    public bool IsAcknowledged => AcknowledgedAt is not null;

    /// <summary>"Open" alarms are shown in the active alarm list: still active, or cleared but not yet acknowledged.</summary>
    public bool IsOpen => IsActive || !IsAcknowledged;

    public AlarmState State => (IsActive, IsAcknowledged) switch
    {
        (true, false) => AlarmState.ActiveUnacknowledged,
        (true, true) => AlarmState.ActiveAcknowledged,
        (false, false) => AlarmState.ClearedUnacknowledged,
        _ => AlarmState.Closed
    };

    public TimeSpan Duration(DateTime now) => (ClearedAt ?? now) - RaisedAt;

    public static Alarm Raise(int unitId, AlarmRule rule, double value, DateTime now) => new()
    {
        UnitId = unitId,
        AlarmRuleId = rule.Id,
        Parameter = rule.Parameter,
        Severity = rule.Severity,
        TriggerValue = value,
        Threshold = rule.Threshold,
        RaisedAt = now,
        Message = FormattableString.Invariant(
            $"{rule.Name}: {value:0.#} {rule.Parameter.Unit()} (limit {rule.Describe()})")
    };

    public void Acknowledge(string user, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            throw new DomainException("A user is required to acknowledge an alarm.");
        }

        if (IsAcknowledged)
        {
            throw new DomainException("Alarm is already acknowledged.");
        }

        AcknowledgedAt = now;
        AcknowledgedBy = user;
    }

    public void Clear(DateTime now)
    {
        if (!IsActive)
        {
            throw new DomainException("Alarm is already cleared.");
        }

        ClearedAt = now;
    }
}
