using System.ComponentModel.DataAnnotations;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Common;
using HvacGuard.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HvacGuard.Web.Models;

public sealed class AlarmListViewModel
{
    public IReadOnlyList<Alarm> Alarms { get; init; } = [];
    public int? VesselId { get; init; }
    public AlarmSeverity? Severity { get; init; }
    public IReadOnlyList<SelectListItem> VesselOptions { get; init; } = [];
}

public sealed class AlarmHistoryViewModel
{
    public required PagedResult<Alarm> Result { get; init; }
    public required AlarmHistoryQuery Query { get; init; }
    public IReadOnlyList<SelectListItem> VesselOptions { get; init; } = [];
}

public sealed class AlarmRuleFormModel
{
    public int? Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public SensorParameter Parameter { get; set; } = SensorParameter.SupplyAirTemperature;

    [Required]
    public ThresholdCondition Condition { get; set; } = ThresholdCondition.Above;

    [Range(-1000.0, 10000.0)]
    public double Threshold { get; set; }

    [Range(0.0, 1000.0), Display(Name = "Deadband (hysteresis)")]
    public double Deadband { get; set; }

    [Required]
    public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;

    [Display(Name = "Enabled")]
    public bool IsEnabled { get; set; } = true;

    public AlarmRuleInput ToInput() => new(Name, Parameter, Condition, Threshold, Deadband, Severity, IsEnabled);

    public static AlarmRuleFormModel From(AlarmRule rule) => new()
    {
        Id = rule.Id,
        Name = rule.Name,
        Parameter = rule.Parameter,
        Condition = rule.Condition,
        Threshold = rule.Threshold,
        Deadband = rule.Deadband,
        Severity = rule.Severity,
        IsEnabled = rule.IsEnabled
    };
}
