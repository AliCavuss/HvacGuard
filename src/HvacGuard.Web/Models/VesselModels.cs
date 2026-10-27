using System.ComponentModel.DataAnnotations;
using HvacGuard.Domain.Entities;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Validation;

namespace HvacGuard.Web.Models;

public sealed class VesselFormModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, ValidImo, Display(Name = "IMO number")]
    public string ImoNumber { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Vessel type")]
    public string VesselType { get; set; } = string.Empty;

    [Required, StringLength(60), Display(Name = "Flag state")]
    public string Flag { get; set; } = string.Empty;

    [StringLength(100), Display(Name = "Owner / operator")]
    public string? Owner { get; set; }

    public VesselInput ToInput() => new(Name, ImoNumber, VesselType, Flag, Owner);

    public static VesselFormModel From(Vessel vessel) => new()
    {
        Id = vessel.Id,
        Name = vessel.Name,
        ImoNumber = vessel.ImoNumber,
        VesselType = vessel.VesselType,
        Flag = vessel.Flag,
        Owner = vessel.Owner
    };
}

public sealed record VesselDetailsViewModel(Vessel Vessel, IReadOnlyList<UnitStatusDto> Units);
