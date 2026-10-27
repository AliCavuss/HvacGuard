using System.ComponentModel.DataAnnotations;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Filters;
using HvacGuard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HvacGuard.Web.Controllers.Api;

/// <summary>
/// REST API for field gateways (PLC / SCADA / IoT edge devices) and for the browser charts.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class TelemetryApiController(ITelemetryService telemetry, IRealtimeNotifier notifier, TimeProvider clock)
    : ControllerBase
{
    /// <summary>Push one telemetry sample. Requires the <c>X-Api-Key</c> header.</summary>
    [HttpPost("telemetry")]
    [AllowAnonymous]
    [ServiceFilter(typeof(ApiKeyFilter))]
    [ProducesResponseType<TelemetryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TelemetryResponse>> Ingest(TelemetryRequest request, CancellationToken ct)
    {
        var result = await telemetry.IngestAsync(new TelemetryInput(
            request.UnitId,
            request.Timestamp,
            request.SupplyAirTemperature,
            request.ReturnAirTemperature,
            request.RelativeHumidity,
            request.FanSpeed,
            request.FilterPressureDrop), ct);

        await notifier.PublishTelemetryAsync(result, ct);

        return Ok(new TelemetryResponse(
            result.Reading.Id,
            result.RaisedAlarms.Select(a => a.Message).ToList(),
            result.ClearedAlarms.Count));
    }

    /// <summary>Readings of a unit for the last <paramref name="minutes"/> minutes (max 24 h).</summary>
    [HttpGet("units/{unitId:int}/readings")]
    [ProducesResponseType<IReadOnlyList<ReadingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReadingDto>>> GetReadings(
        int unitId, [FromQuery, Range(1, 1440)] int minutes = 30, CancellationToken ct = default)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddMinutes(-minutes);
        return Ok(await telemetry.GetReadingsAsync(unitId, since, ct));
    }
}

public sealed record TelemetryRequest
{
    [Range(1, int.MaxValue)]
    public int UnitId { get; init; }

    /// <summary>Sample time (UTC). Defaults to the time the server receives the request.</summary>
    public DateTime? Timestamp { get; init; }

    [Range(-50.0, 100.0)]
    public double SupplyAirTemperature { get; init; }

    [Range(-50.0, 100.0)]
    public double ReturnAirTemperature { get; init; }

    [Range(0.0, 100.0)]
    public double RelativeHumidity { get; init; }

    [Range(0.0, 100.0)]
    public double FanSpeed { get; init; }

    [Range(0.0, 5000.0)]
    public double FilterPressureDrop { get; init; }
}

public sealed record TelemetryResponse(long ReadingId, IReadOnlyList<string> RaisedAlarms, int ClearedAlarms);
