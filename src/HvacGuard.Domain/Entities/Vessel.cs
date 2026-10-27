namespace HvacGuard.Domain.Entities;

public class Vessel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>7-digit IMO number (validated with its check digit).</summary>
    public string ImoNumber { get; set; } = string.Empty;

    public string VesselType { get; set; } = string.Empty;
    public string Flag { get; set; } = string.Empty;
    public string? Owner { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<AirHandlingUnit> Units { get; set; } = new List<AirHandlingUnit>();
}
