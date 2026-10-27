using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HvacGuard.Web.Hubs;

/// <summary>
/// Pushes live readings and alarm events to the browser. Server → client only.
/// Client events: "reading", "alarmRaised", "alarmCleared", "alarmAcknowledged".
/// </summary>
[Authorize]
public sealed class TelemetryHub : Hub
{
}

public sealed record ReadingMessage(
    int UnitId,
    DateTime Timestamp,
    double SupplyAirTemperature,
    double ReturnAirTemperature,
    double RelativeHumidity,
    double FanSpeed,
    double FilterPressureDrop);

public sealed record AlarmMessage(
    long Id,
    int UnitId,
    string UnitTag,
    string VesselName,
    string Severity,
    string Message,
    DateTime RaisedAt);

public sealed record AlarmStateMessage(long Id, int UnitId, string? User);
