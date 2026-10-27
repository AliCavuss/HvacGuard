using HvacGuard.Infrastructure.Data;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HvacGuard.Web.Services;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 3;

    /// <summary>Readings older than this are purged periodically.</summary>
    public int RetentionHours { get; set; } = 24;
}

/// <summary>
/// Background service that plays the role of the field devices: every few seconds it generates a reading for each
/// active AHU, sends it through the same ingestion pipeline as the public telemetry API and broadcasts it via SignalR.
/// </summary>
public sealed class TelemetrySimulator(
    IServiceScopeFactory scopeFactory,
    IRealtimeNotifier notifier,
    IOptions<SimulatorOptions> options,
    TimeProvider clock,
    ILogger<TelemetrySimulator> logger) : BackgroundService
{
    private const int PurgeEveryTicks = 200;
    private readonly Dictionary<int, AhuSimulator> _models = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Telemetry simulator is disabled.");
            return;
        }

        logger.LogInformation("Telemetry simulator started (interval {Interval}s).", settings.IntervalSeconds);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, settings.IntervalSeconds)));

        var tick = 0L;
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await TickAsync(stoppingToken);

                    if (++tick % PurgeEveryTicks == 0)
                    {
                        await PurgeAsync(settings.RetentionHours, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Telemetry simulation tick failed.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Application is shutting down.
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();

        var units = await db.Units.AsNoTracking()
            .Where(u => u.IsActive)
            .Select(u => new { u.Id, u.SupplyTempSetpoint })
            .ToListAsync(ct);

        foreach (var unit in units)
        {
            if (!_models.TryGetValue(unit.Id, out var model))
            {
                model = new AhuSimulator(unit.Id, unit.SupplyTempSetpoint);
                _models[unit.Id] = model;
            }

            var sample = model.Next();
            var result = await telemetry.IngestAsync(new TelemetryInput(
                unit.Id,
                null,
                sample.SupplyAirTemperature,
                sample.ReturnAirTemperature,
                sample.RelativeHumidity,
                sample.FanSpeed,
                sample.FilterPressureDrop), ct);

            await notifier.PublishTelemetryAsync(result, ct);
        }
    }

    private async Task PurgeAsync(int retentionHours, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();
        var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-Math.Max(1, retentionHours));
        var deleted = await telemetry.PurgeAsync(cutoff, ct);
        if (deleted > 0)
        {
            logger.LogInformation("Purged {Count} readings older than {Cutoff:u}.", deleted, cutoff);
        }
    }
}
