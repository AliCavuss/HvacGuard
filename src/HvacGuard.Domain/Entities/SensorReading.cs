using HvacGuard.Domain.Enums;

namespace HvacGuard.Domain.Entities;

/// <summary>
/// One telemetry sample of an AHU. All values are stored in SI-style engineering units.
/// </summary>
public class SensorReading
{
    public long Id { get; set; }

    public int UnitId { get; set; }
    public AirHandlingUnit Unit { get; set; } = null!;

    /// <summary>Sample time (UTC).</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>°C</summary>
    public double SupplyAirTemperature { get; set; }

    /// <summary>°C</summary>
    public double ReturnAirTemperature { get; set; }

    /// <summary>%RH</summary>
    public double RelativeHumidity { get; set; }

    /// <summary>% of nominal speed</summary>
    public double FanSpeed { get; set; }

    /// <summary>Differential pressure across the filter in Pa (rises as the filter clogs).</summary>
    public double FilterPressureDrop { get; set; }

    public double GetValue(SensorParameter parameter) => parameter switch
    {
        SensorParameter.SupplyAirTemperature => SupplyAirTemperature,
        SensorParameter.ReturnAirTemperature => ReturnAirTemperature,
        SensorParameter.RelativeHumidity => RelativeHumidity,
        SensorParameter.FanSpeed => FanSpeed,
        SensorParameter.FilterPressureDrop => FilterPressureDrop,
        _ => throw new ArgumentOutOfRangeException(nameof(parameter), parameter, "Unknown sensor parameter.")
    };
}
