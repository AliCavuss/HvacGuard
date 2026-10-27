namespace HvacGuard.Domain.Entities;

/// <summary>
/// An Air Handling Unit (AHU) installed on board a vessel.
/// </summary>
public class AirHandlingUnit
{
    public int Id { get; set; }

    public int VesselId { get; set; }
    public Vessel Vessel { get; set; } = null!;

    /// <summary>Equipment tag, unique per vessel (e.g. "AHU-01").</summary>
    public string Tag { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Where the unit is installed / which zone it serves (e.g. "Deck 7 – Passenger lounge").</summary>
    public string Location { get; set; } = string.Empty;

    public string? Manufacturer { get; set; }

    /// <summary>Rated airflow in m³/h.</summary>
    public int RatedAirflow { get; set; }

    /// <summary>Supply air temperature setpoint in °C.</summary>
    public double SupplyTempSetpoint { get; set; } = 18;

    public DateTime? CommissionedOn { get; set; }

    /// <summary>Inactive units are not monitored (e.g. dry-dock, decommissioned).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Timestamp (UTC) of the last telemetry sample received.</summary>
    public DateTime? LastSeenAt { get; set; }

    public ICollection<SensorReading> Readings { get; set; } = new List<SensorReading>();
    public ICollection<Alarm> Alarms { get; set; } = new List<Alarm>();
    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();

    public string DisplayName => $"{Tag} – {Name}";
}
