using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Data;
using HvacGuard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace HvacGuard.Tests;

/// <summary>Integration-style tests of the ingestion pipeline against the EF Core in-memory provider.</summary>
public class TelemetryServiceTests
{
    private static async Task<(ApplicationDbContext Db, TelemetryService Service, int UnitId)> CreateAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var db = new ApplicationDbContext(options);
        var vessel = new Vessel { Name = "Test Vessel", ImoNumber = "9074729", VesselType = "Tanker", Flag = "Malta" };
        var unit = new AirHandlingUnit { Tag = "AHU-01", Name = "Accommodation", Location = "A-Deck", Vessel = vessel };
        db.Units.Add(unit);
        db.AlarmRules.Add(new AlarmRule
        {
            Name = "Supply air temperature high", Parameter = SensorParameter.SupplyAirTemperature,
            Condition = ThresholdCondition.Above, Threshold = 22, Deadband = 0.5, Severity = AlarmSeverity.Warning
        });
        await db.SaveChangesAsync();

        var service = new TelemetryService(db, TimeProvider.System, NullLogger<TelemetryService>.Instance);
        return (db, service, unit.Id);
    }

    private static TelemetryInput Sample(int unitId, double supply) =>
        new(unitId, null, supply, supply + 5, 50, 78, 150);

    [Fact]
    public async Task Ingest_stores_reading_and_updates_last_seen()
    {
        var (db, service, unitId) = await CreateAsync();

        var result = await service.IngestAsync(Sample(unitId, 18));

        Assert.Equal(1, await db.SensorReadings.CountAsync());
        Assert.Empty(result.RaisedAlarms);
        Assert.Equal("AHU-01", result.UnitTag);
        Assert.NotNull((await db.Units.SingleAsync()).LastSeenAt);
    }

    [Fact]
    public async Task Alarm_is_raised_once_and_cleared_after_recovery()
    {
        var (db, service, unitId) = await CreateAsync();

        var first = await service.IngestAsync(Sample(unitId, 24));
        var second = await service.IngestAsync(Sample(unitId, 24.5));
        var recovered = await service.IngestAsync(Sample(unitId, 19));

        Assert.Single(first.RaisedAlarms);
        Assert.Empty(second.RaisedAlarms);
        Assert.Single(recovered.ClearedAlarms);

        var alarm = await db.Alarms.SingleAsync();
        Assert.NotNull(alarm.ClearedAt);
        Assert.Equal(AlarmState.ClearedUnacknowledged, alarm.State);
    }

    [Fact]
    public async Task Inactive_unit_does_not_raise_alarms()
    {
        var (db, service, unitId) = await CreateAsync();
        (await db.Units.SingleAsync()).IsActive = false;
        await db.SaveChangesAsync();

        var result = await service.IngestAsync(Sample(unitId, 30));

        Assert.Empty(result.RaisedAlarms);
        Assert.Equal(0, await db.Alarms.CountAsync());
    }
}
