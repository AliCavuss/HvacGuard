using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;

namespace HvacGuard.Tests;

public class AlarmRuleTests
{
    private static AlarmRule High(double threshold = 22, double deadband = 0.5) => new()
    {
        Id = 1, Name = "High", Parameter = SensorParameter.SupplyAirTemperature,
        Condition = ThresholdCondition.Above, Threshold = threshold, Deadband = deadband, Severity = AlarmSeverity.Warning
    };

    private static AlarmRule Low(double threshold = 20, double deadband = 5) => new()
    {
        Id = 2, Name = "Low", Parameter = SensorParameter.FanSpeed,
        Condition = ThresholdCondition.Below, Threshold = threshold, Deadband = deadband, Severity = AlarmSeverity.Critical
    };

    [Theory]
    [InlineData(22.1, true)]
    [InlineData(22.0, false)] // the limit itself is not a violation
    [InlineData(18.0, false)]
    public void Above_rule_detects_violation(double value, bool expected) =>
        Assert.Equal(expected, High().IsViolatedBy(value));

    [Theory]
    [InlineData(19.9, true)]
    [InlineData(20.0, false)]
    public void Below_rule_detects_violation(double value, bool expected) =>
        Assert.Equal(expected, Low().IsViolatedBy(value));

    [Theory]
    [InlineData(21.8, false)] // inside the deadband – still in alarm
    [InlineData(21.5, true)]  // threshold - deadband
    [InlineData(19.0, true)]
    public void Above_rule_returns_to_normal_only_outside_deadband(double value, bool expected) =>
        Assert.Equal(expected, High().HasReturnedToNormal(value));

    [Theory]
    [InlineData(22.0, false)]
    [InlineData(25.0, true)]
    public void Below_rule_returns_to_normal_only_outside_deadband(double value, bool expected) =>
        Assert.Equal(expected, Low().HasReturnedToNormal(value));
}
