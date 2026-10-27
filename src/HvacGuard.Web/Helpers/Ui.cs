using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using HvacGuard.Domain.Enums;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HvacGuard.Web.Helpers;

/// <summary>Small presentation helpers used by the Razor views.</summary>
public static class Ui
{
    public static string SeverityBadge(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => "text-bg-danger",
        AlarmSeverity.Warning => "text-bg-warning",
        _ => "text-bg-info"
    };

    public static string SeverityIcon(AlarmSeverity severity) => severity switch
    {
        AlarmSeverity.Critical => "bi-exclamation-octagon-fill",
        AlarmSeverity.Warning => "bi-exclamation-triangle-fill",
        _ => "bi-info-circle-fill"
    };

    public static string AlarmStateBadge(AlarmState state) => state switch
    {
        AlarmState.ActiveUnacknowledged => "text-bg-danger blink",
        AlarmState.ActiveAcknowledged => "text-bg-warning",
        AlarmState.ClearedUnacknowledged => "text-bg-info",
        _ => "text-bg-secondary"
    };

    public static string StatusBadge(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Open => "text-bg-primary",
        WorkOrderStatus.InProgress => "text-bg-info",
        WorkOrderStatus.OnHold => "text-bg-warning",
        WorkOrderStatus.Completed => "text-bg-success",
        _ => "text-bg-secondary"
    };

    public static string PriorityBadge(WorkOrderPriority priority) => priority switch
    {
        WorkOrderPriority.Urgent => "text-bg-danger",
        WorkOrderPriority.High => "text-bg-warning",
        WorkOrderPriority.Medium => "text-bg-primary",
        _ => "text-bg-secondary"
    };

    /// <summary>CSS state class for a unit card: offline / critical / warning / normal.</summary>
    public static string UnitStateClass(bool isActive, bool isOnline, AlarmSeverity? highest) =>
        !isActive ? "unit-inactive"
        : !isOnline ? "unit-offline"
        : highest switch
        {
            AlarmSeverity.Critical => "unit-critical",
            AlarmSeverity.Warning => "unit-warning",
            _ => "unit-normal"
        };

    public static string Utc(DateTime? value, string format = "yyyy-MM-dd HH:mm") =>
        value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) + " UTC" : "—";

    public static string Date(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "—";

    public static string Ago(DateTime? value, DateTime now)
    {
        if (!value.HasValue) return "never";
        var span = now - value.Value;
        if (span.TotalSeconds < 60) return $"{Math.Max(0, (int)span.TotalSeconds)}s ago";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} h ago";
        return $"{(int)span.TotalDays} d ago";
    }

    public static string Duration(TimeSpan span) =>
        span.TotalMinutes < 1 ? $"{(int)span.TotalSeconds}s"
        : span.TotalHours < 1 ? $"{(int)span.TotalMinutes} min"
        : span.TotalDays < 1 ? $"{(int)span.TotalHours} h {span.Minutes} min"
        : $"{(int)span.TotalDays} d {span.Hours} h";

    public static string Value(double? value, SensorParameter parameter, string format = "0.0") =>
        value.HasValue
            ? value.Value.ToString(format, CultureInfo.InvariantCulture) + " " + parameter.Unit()
            : "—";

    /// <summary>Reads the [Display(Name = ...)] of an enum value, falling back to its name.</summary>
    public static string DisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        return member?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? value.ToString();
    }

    /// <summary>Options for an enum drop-down; values are the enum names so they bind and round-trip in URLs.</summary>
    public static IReadOnlyList<SelectListItem> EnumOptions<TEnum>(TEnum? selected = null) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>()
            .Select(v => new SelectListItem(
                DisplayName(v),
                v.ToString(),
                selected.HasValue && EqualityComparer<TEnum>.Default.Equals(v, selected.Value)))
            .ToList();

    /// <summary>Current URL with a single query string value replaced (used by pagers and sort links).</summary>
    public static string WithQuery(this HttpRequest request, string key, string value)
    {
        var builder = new QueryBuilder(
            request.Query
                .Where(q => !string.Equals(q.Key, key, StringComparison.OrdinalIgnoreCase))
                .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? string.Empty))));
        builder.Add(key, value);
        return request.PathBase + request.Path + builder.ToQueryString();
    }
}
