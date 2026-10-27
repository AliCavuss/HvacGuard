using System.ComponentModel.DataAnnotations;
using HvacGuard.Domain.Entities;
using HvacGuard.Infrastructure.Services;

namespace HvacGuard.Web.Models;

public sealed class UnitFormModel
{
    public int? Id { get; set; }

    [Required]
    public int VesselId { get; set; }

    public string? VesselName { get; set; }

    [Required, StringLength(20)]
    [RegularExpression(@"^[A-Za-z0-9\-]+$", ErrorMessage = "Tag may only contain letters, digits and dashes.")]
    [Display(Name = "Equipment tag")]
    public string Tag { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Location / zone")]
    public string Location { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [Range(100, 200_000), Display(Name = "Rated airflow (m³/h)")]
    public int RatedAirflow { get; set; } = 5000;

    [Range(10.0, 30.0), Display(Name = "Supply air setpoint (°C)")]
    public double SupplyTempSetpoint { get; set; } = 18;

    [DataType(DataType.Date), Display(Name = "Commissioned on")]
    public DateTime? CommissionedOn { get; set; }

    [Display(Name = "Active (monitored)")]
    public bool IsActive { get; set; } = true;

    public UnitInput ToInput() => new(VesselId, Tag, Name, Location, Manufacturer, RatedAirflow,
        SupplyTempSetpoint, CommissionedOn, IsActive);

    public static UnitFormModel From(AirHandlingUnit unit) => new()
    {
        Id = unit.Id,
        VesselId = unit.VesselId,
        VesselName = unit.Vessel?.Name,
        Tag = unit.Tag,
        Name = unit.Name,
        Location = unit.Location,
        Manufacturer = unit.Manufacturer,
        RatedAirflow = unit.RatedAirflow,
        SupplyTempSetpoint = unit.SupplyTempSetpoint,
        CommissionedOn = unit.CommissionedOn,
        IsActive = unit.IsActive
    };
}

public sealed class UnitDetailsViewModel
{
    public required AirHandlingUnit Unit { get; init; }
    public required UnitStatusDto Status { get; init; }
    public IReadOnlyList<Alarm> OpenAlarms { get; init; } = [];
    public IReadOnlyList<WorkOrder> WorkOrders { get; init; } = [];
    public IReadOnlyList<AlarmRule> Rules { get; init; } = [];
}
