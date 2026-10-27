using HvacGuard.Domain.Entities;
using HvacGuard.Infrastructure.Services;
using HvacGuard.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HvacGuard.Web.Services;

public interface IRealtimeNotifier
{
    Task PublishTelemetryAsync(TelemetryIngestResult result, CancellationToken ct = default);
    Task PublishAcknowledgedAsync(IEnumerable<Alarm> alarms, string user, CancellationToken ct = default);
}

public sealed class SignalRRealtimeNotifier(IHubContext<TelemetryHub> hub) : IRealtimeNotifier
{
    public async Task PublishTelemetryAsync(TelemetryIngestResult result, CancellationToken ct = default)
    {
        var r = result.Reading;
        await hub.Clients.All.SendAsync("reading", new ReadingMessage(
            r.UnitId, r.Timestamp, r.SupplyAirTemperature, r.ReturnAirTemperature,
            r.RelativeHumidity, r.FanSpeed, r.FilterPressureDrop), ct);

        foreach (var alarm in result.RaisedAlarms)
        {
            await hub.Clients.All.SendAsync("alarmRaised", new AlarmMessage(
                alarm.Id, alarm.UnitId, result.UnitTag, result.VesselName,
                alarm.Severity.ToString(), alarm.Message, alarm.RaisedAt), ct);
        }

        foreach (var alarm in result.ClearedAlarms)
        {
            await hub.Clients.All.SendAsync("alarmCleared", new AlarmStateMessage(alarm.Id, alarm.UnitId, null), ct);
        }
    }

    public async Task PublishAcknowledgedAsync(IEnumerable<Alarm> alarms, string user, CancellationToken ct = default)
    {
        foreach (var alarm in alarms)
        {
            await hub.Clients.All.SendAsync("alarmAcknowledged", new AlarmStateMessage(alarm.Id, alarm.UnitId, user), ct);
        }
    }
}
