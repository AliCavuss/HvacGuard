using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;

namespace HvacGuard.Tests;

public class AlarmTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Alarm NewAlarm() => Alarm.Raise(1, new AlarmRule
    {
        Id = 7, Name = "Filter clogged", Parameter = SensorParameter.FilterPressureDrop,
        Condition = ThresholdCondition.Above, Threshold = 250, Severity = AlarmSeverity.Warning
    }, 263.4, Now);

    [Fact]
    public void Raise_copies_rule_information_and_builds_message()
    {
        var alarm = NewAlarm();

        Assert.Equal(7, alarm.AlarmRuleId);
        Assert.Equal(AlarmSeverity.Warning, alarm.Severity);
        Assert.Equal(250, alarm.Threshold);
        Assert.Equal(AlarmState.ActiveUnacknowledged, alarm.State);
        Assert.Contains("263.4 Pa", alarm.Message);
    }

    [Fact]
    public void Alarm_follows_isa_18_2_life_cycle()
    {
        var alarm = NewAlarm();

        alarm.Acknowledge("engineer@hvacguard.local", Now.AddMinutes(1));
        Assert.Equal(AlarmState.ActiveAcknowledged, alarm.State);
        Assert.True(alarm.IsOpen);

        alarm.Clear(Now.AddMinutes(5));
        Assert.Equal(AlarmState.Closed, alarm.State);
        Assert.False(alarm.IsOpen);
        Assert.Equal(TimeSpan.FromMinutes(5), alarm.Duration(Now.AddHours(1)));
    }

    [Fact]
    public void Cleared_but_unacknowledged_alarm_stays_open()
    {
        var alarm = NewAlarm();
        alarm.Clear(Now.AddMinutes(2));

        Assert.Equal(AlarmState.ClearedUnacknowledged, alarm.State);
        Assert.True(alarm.IsOpen);
    }

    [Fact]
    public void Acknowledging_twice_throws()
    {
        var alarm = NewAlarm();
        alarm.Acknowledge("a", Now);

        Assert.Throws<DomainException>(() => alarm.Acknowledge("b", Now));
    }

    [Fact]
    public void Acknowledge_requires_user() =>
        Assert.Throws<DomainException>(() => NewAlarm().Acknowledge(" ", Now));
}
