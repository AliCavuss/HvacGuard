using HvacGuard.Domain.Enums;

namespace HvacGuard.Domain.Entities;

/// <summary>
/// A threshold rule evaluated against every incoming sensor reading.
/// A deadband (hysteresis) prevents alarm "chattering" when a value oscillates around the threshold.
/// </summary>
public class AlarmRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SensorParameter Parameter { get; set; }
    public ThresholdCondition Condition { get; set; }
    public double Threshold { get; set; }

    /// <summary>The value must move this far back past the threshold before the alarm clears.</summary>
    public double Deadband { get; set; }

    public AlarmSeverity Severity { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool IsViolatedBy(double value) => Condition switch
    {
        ThresholdCondition.Above => value > Threshold,
        ThresholdCondition.Below => value < Threshold,
        _ => false
    };

    public bool HasReturnedToNormal(double value) => Condition switch
    {
        ThresholdCondition.Above => value <= Threshold - Deadband,
        ThresholdCondition.Below => value >= Threshold + Deadband,
        _ => true
    };

    public string Describe() =>
        FormattableString.Invariant(
            $"{(Condition == ThresholdCondition.Above ? ">" : "<")} {Threshold:0.##} {Parameter.Unit()}");
}
