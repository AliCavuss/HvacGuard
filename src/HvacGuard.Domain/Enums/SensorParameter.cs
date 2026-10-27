using System.ComponentModel.DataAnnotations;

namespace HvacGuard.Domain.Enums;

public enum SensorParameter
{
    [Display(Name = "Supply air temperature")]
    SupplyAirTemperature = 1,

    [Display(Name = "Return air temperature")]
    ReturnAirTemperature = 2,

    [Display(Name = "Relative humidity")]
    RelativeHumidity = 3,

    [Display(Name = "Fan speed")]
    FanSpeed = 4,

    [Display(Name = "Filter differential pressure")]
    FilterPressureDrop = 5
}

public static class SensorParameterExtensions
{
    public static string Unit(this SensorParameter parameter) => parameter switch
    {
        SensorParameter.SupplyAirTemperature or SensorParameter.ReturnAirTemperature => "°C",
        SensorParameter.RelativeHumidity => "%RH",
        SensorParameter.FanSpeed => "%",
        SensorParameter.FilterPressureDrop => "Pa",
        _ => string.Empty
    };
}
