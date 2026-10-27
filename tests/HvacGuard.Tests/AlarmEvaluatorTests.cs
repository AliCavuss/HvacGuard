using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Domain.Services;

namespace HvacGuard.Tests;

public class AlarmEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly AlarmRule SupplyHigh = new()
    {
        Id = 1, Name = "Supply high", Parameter = SensorParameter.SupplyAirTemperature,
        Condition = ThresholdCondition.Above, Threshold = 22, Deadband = 0.5, Severity = AlarmSeverity.Warning
    };

    private static readonly AlarmRule FanFailure = new()
    {
        Id = 2, Name = "Fan failure", Parameter = SensorParameter.FanSpeed,
        Condition = ThresholdCondition.Below, Threshold = 20, Deadband = 5, Severity = AlarmSeverity.Critical
    };

    private static SensorReading Reading(double supply = 18, double fan = 78) => new()
    {
        UnitId = 1, Timestamp = Now, SupplyAirTemperature = supply, ReturnAirTemperature = 24,
        RelativeHumidity = 50, FanSpeed = fan, FilterPressureDrop = 150
    };

    [Fact]
    public void Normal_reading_raises_nothing()
    {
        var result = AlarmEvaluator.Evaluate(Reading(), [SupplyHigh, FanFailure], [], Now);

        Assert.Empty(result.Raised);
        Assert.Empty(result.Cleared);
    }

    [Fact]
    public void Violations_raise_one_alarm_per_rule()
    {
        var result = AlarmEvaluator.Evaluate(Reading(supply: 24, fan: 5), [SupplyHigh, FanFailure], [], Now);

        Assert.Equal(2, result.Raised.Count);
        Assert.Contains(result.Raised, a => a.AlarmRuleId == SupplyHigh.Id && a.Severity == AlarmSeverity.Warning);
        Assert.Contains(result.Raised, a => a.AlarmRuleId == FanFailure.Id && a.Severity == AlarmSeverity.Critical);
    }

    [Fact]
    public void Existing_active_alarm_is_not_duplicated()
    {
        var active = Alarm.Raise(1, SupplyHigh, 23, Now.AddMinutes(-1));

        var result = AlarmEvaluator.Evaluate(Reading(supply: 23.5), [SupplyHigh], [active], Now);

        Assert.Empty(result.Raised);
        Assert.Empty(result.Cleared);
    }

    [Fact]
    public void Alarm_stays_active_inside_deadband()
    {
        var active = Alarm.Raise(1, SupplyHigh, 23, Now.AddMinutes(-1));

        var result = AlarmEvaluator.Evaluate(Reading(supply: 21.8), [SupplyHigh], [active], Now);

        Assert.Empty(result.Cleared);
        Assert.True(active.IsActive);
    }

    [Fact]
    public void Alarm_clears_when_value_returns_below_deadband()
    {
        var active = Alarm.Raise(1, SupplyHigh, 23, Now.AddMinutes(-1));

        var result = AlarmEvaluator.Evaluate(Reading(supply: 21.0), [SupplyHigh], [active], Now);

        Assert.Single(result.Cleared);
        Assert.Equal(Now, active.ClearedAt);
    }

    [Fact]
    public void Alarm_of_disabled_rule_is_cleared()
    {
        var disabled = new AlarmRule
        {
            Id = SupplyHigh.Id, Name = SupplyHigh.Name, Parameter = SupplyHigh.Parameter, Condition = SupplyHigh.Condition,
            Threshold = SupplyHigh.Threshold, Deadband = SupplyHigh.Deadband, Severity = SupplyHigh.Severity, IsEnabled = false
        };
        var active = Alarm.Raise(1, SupplyHigh, 23, Now.AddMinutes(-1));

        var result = AlarmEvaluator.Evaluate(Reading(supply: 24), [disabled], [active], Now);

        Assert.Single(result.Cleared);
        Assert.Empty(result.Raised);
    }
}
