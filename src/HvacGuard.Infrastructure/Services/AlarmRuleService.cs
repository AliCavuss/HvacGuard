using HvacGuard.Domain.Common;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Services;

public sealed record AlarmRuleInput(
    string Name,
    SensorParameter Parameter,
    ThresholdCondition Condition,
    double Threshold,
    double Deadband,
    AlarmSeverity Severity,
    bool IsEnabled);

public interface IAlarmRuleService
{
    Task<IReadOnlyList<AlarmRule>> GetAllAsync(CancellationToken ct = default);
    Task<AlarmRule> GetAsync(int id, CancellationToken ct = default);
    Task<AlarmRule> CreateAsync(AlarmRuleInput input, CancellationToken ct = default);
    Task UpdateAsync(int id, AlarmRuleInput input, CancellationToken ct = default);
    Task<bool> ToggleAsync(int id, CancellationToken ct = default);
}

public sealed class AlarmRuleService(ApplicationDbContext db) : IAlarmRuleService
{
    public async Task<IReadOnlyList<AlarmRule>> GetAllAsync(CancellationToken ct = default)
    {
        // Enum columns are stored as strings, so sort in memory to get the logical order.
        var rules = await db.AlarmRules.AsNoTracking().ToListAsync(ct);
        return rules.OrderBy(r => r.Parameter).ThenByDescending(r => r.Severity).ThenBy(r => r.Threshold).ToList();
    }

    public async Task<AlarmRule> GetAsync(int id, CancellationToken ct = default) =>
        await db.AlarmRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Alarm rule", id);

    public async Task<AlarmRule> CreateAsync(AlarmRuleInput input, CancellationToken ct = default)
    {
        var rule = new AlarmRule();
        Apply(rule, input);
        db.AlarmRules.Add(rule);
        await db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task UpdateAsync(int id, AlarmRuleInput input, CancellationToken ct = default)
    {
        var rule = await db.AlarmRules.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Alarm rule", id);
        Apply(rule, input);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ToggleAsync(int id, CancellationToken ct = default)
    {
        var rule = await db.AlarmRules.FindAsync(new object[] { id }, ct) ?? throw new NotFoundException("Alarm rule", id);
        rule.IsEnabled = !rule.IsEnabled;
        await db.SaveChangesAsync(ct);
        return rule.IsEnabled;
    }

    private static void Apply(AlarmRule rule, AlarmRuleInput input)
    {
        if (input.Deadband < 0)
        {
            throw new DomainException("Deadband cannot be negative.");
        }

        rule.Name = input.Name.Trim();
        rule.Parameter = input.Parameter;
        rule.Condition = input.Condition;
        rule.Threshold = input.Threshold;
        rule.Deadband = input.Deadband;
        rule.Severity = input.Severity;
        rule.IsEnabled = input.IsEnabled;
    }
}
