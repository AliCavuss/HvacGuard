using HvacGuard.Domain.Constants;
using HvacGuard.Domain.Entities;
using HvacGuard.Domain.Enums;
using HvacGuard.Infrastructure.Identity;
using HvacGuard.Infrastructure.Simulation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HvacGuard.Infrastructure.Data;

/// <summary>
/// Seeds roles, demo users, default alarm rules and a demo fleet (fictional vessels) with one hour of telemetry history.
/// </summary>
public static class DbSeeder
{
    public static readonly (string Email, string Password, string FullName, string Role)[] DemoUsers =
    [
        ("admin@hvacguard.local", "Admin123!", "Fleet Administrator", Roles.Admin),
        ("engineer@hvacguard.local", "Engineer123!", "Deniz Yılmaz", Roles.Engineer),
        ("tech@hvacguard.local", "Engineer123!", "Can Aydın", Roles.Engineer),
        ("viewer@hvacguard.local", "Viewer123!", "Owner Representative", Roles.Viewer)
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var clock = services.GetRequiredService<TimeProvider>();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager);

        if (!await db.AlarmRules.AnyAsync(ct))
        {
            db.AlarmRules.AddRange(DefaultAlarmRules());
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Vessels.AnyAsync(ct))
        {
            await SeedFleetAsync(db, clock.GetUtcNow().UtcDateTime, ct);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        foreach (var (email, password, fullName, role) in DemoUsers)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            await userManager.AddToRoleAsync(user, role);
        }
    }

    public static IEnumerable<AlarmRule> DefaultAlarmRules() =>
    [
        Rule("Supply air temperature high", SensorParameter.SupplyAirTemperature, ThresholdCondition.Above, 22, 0.5, AlarmSeverity.Warning),
        Rule("Supply air temperature high-high", SensorParameter.SupplyAirTemperature, ThresholdCondition.Above, 25, 0.5, AlarmSeverity.Critical),
        Rule("Supply air temperature low", SensorParameter.SupplyAirTemperature, ThresholdCondition.Below, 12, 0.5, AlarmSeverity.Warning),
        Rule("Return air temperature high", SensorParameter.ReturnAirTemperature, ThresholdCondition.Above, 30, 0.5, AlarmSeverity.Warning),
        Rule("Relative humidity high", SensorParameter.RelativeHumidity, ThresholdCondition.Above, 65, 2, AlarmSeverity.Warning),
        Rule("Supply fan failure", SensorParameter.FanSpeed, ThresholdCondition.Below, 20, 5, AlarmSeverity.Critical),
        Rule("Filter clogged – replacement due", SensorParameter.FilterPressureDrop, ThresholdCondition.Above, 250, 10, AlarmSeverity.Warning),
        Rule("Filter clogged – critical", SensorParameter.FilterPressureDrop, ThresholdCondition.Above, 320, 10, AlarmSeverity.Critical)
    ];

    private static AlarmRule Rule(string name, SensorParameter parameter, ThresholdCondition condition,
        double threshold, double deadband, AlarmSeverity severity) => new()
    {
        Name = name,
        Parameter = parameter,
        Condition = condition,
        Threshold = threshold,
        Deadband = deadband,
        Severity = severity,
        IsEnabled = true
    };

    private static async Task SeedFleetAsync(ApplicationDbContext db, DateTime now, CancellationToken ct)
    {
        // Fictional vessels – IMO numbers are syntactically valid (check digit) but made up.
        var vessels = new List<Vessel>
        {
            new()
            {
                Name = "Aurora Spirit", ImoNumber = "9345128", VesselType = "Ro-Pax Ferry", Flag = "Malta",
                Owner = "Aurora Lines", CreatedAt = now.AddDays(-120),
                Units =
                {
                    Unit("AHU-01", "Passenger lounge", "Deck 7 – Fwd", 18, 12000),
                    Unit("AHU-02", "Main restaurant", "Deck 6 – Mid", 17.5, 15000),
                    Unit("AHU-03", "Cabins forward", "Deck 5 – Fwd", 19, 9000),
                    Unit("AHU-04", "Engine control room", "Deck 2 – Aft", 16, 6000)
                }
            },
            new()
            {
                Name = "Northern Star", ImoNumber = "9481271", VesselType = "LNG Carrier", Flag = "Marshall Islands",
                Owner = "Polar Gas Shipping", CreatedAt = now.AddDays(-90),
                Units =
                {
                    Unit("AHU-01", "Accommodation", "A-Deck", 18, 10000),
                    Unit("AHU-02", "Wheelhouse", "Navigation bridge", 19, 4000),
                    Unit("AHU-03", "Engine control room", "Engine room – Upper", 16.5, 5000)
                }
            },
            new()
            {
                Name = "Baltic Pearl", ImoNumber = "9620384", VesselType = "Chemical Tanker", Flag = "Türkiye",
                Owner = "Pearl Tankers", CreatedAt = now.AddDays(-30),
                Units =
                {
                    Unit("AHU-01", "Accommodation", "B-Deck", 18, 8000),
                    Unit("AHU-02", "Galley", "Main deck", 17, 5500),
                    Unit("AHU-03", "Wheelhouse", "Navigation bridge", 19, 3500)
                }
            }
        };

        db.Vessels.AddRange(vessels);
        await db.SaveChangesAsync(ct);

        var units = vessels.SelectMany(v => v.Units).ToList();
        var rules = await db.AlarmRules.ToListAsync(ct);

        // One hour of history at 30 s resolution so charts are populated immediately.
        foreach (var unit in units)
        {
            var model = new AhuSimulator(unit.Id, unit.SupplyTempSetpoint, faultProbability: 0);
            for (var i = 120; i >= 1; i--)
            {
                var s = model.Next();
                db.SensorReadings.Add(new SensorReading
                {
                    UnitId = unit.Id,
                    Timestamp = now.AddSeconds(-30 * i),
                    SupplyAirTemperature = s.SupplyAirTemperature,
                    ReturnAirTemperature = s.ReturnAirTemperature,
                    RelativeHumidity = s.RelativeHumidity,
                    FanSpeed = s.FanSpeed,
                    FilterPressureDrop = s.FilterPressureDrop
                });
            }

            unit.LastSeenAt = now.AddSeconds(-30);
        }

        // Alarm history (closed alarms) for the history / reporting pages.
        var random = new Random(42);
        var history = new List<Alarm>();
        for (var i = 0; i < 40; i++)
        {
            var unit = units[random.Next(units.Count)];
            var rule = rules[random.Next(rules.Count)];
            var raisedAt = now.AddDays(-random.Next(1, 14)).AddMinutes(-random.Next(0, 1440));
            var value = rule.Condition == ThresholdCondition.Above
                ? rule.Threshold + 0.5 + random.NextDouble() * 3
                : rule.Threshold - 0.5 - random.NextDouble() * 3;

            var alarm = Alarm.Raise(unit.Id, rule, Math.Round(value, 1), raisedAt);
            alarm.Acknowledge(random.Next(2) == 0 ? "engineer@hvacguard.local" : "tech@hvacguard.local",
                raisedAt.AddMinutes(random.Next(1, 20)));
            alarm.Clear(raisedAt.AddMinutes(random.Next(20, 180)));
            history.Add(alarm);
        }

        db.Alarms.AddRange(history);
        await db.SaveChangesAsync(ct);

        // Work orders in different workflow states.
        var filterAlarm = history.FirstOrDefault(a => a.Parameter == SensorParameter.FilterPressureDrop) ?? history[0];

        var wo1 = new WorkOrder
        {
            Title = "Replace F7 bag filters", Description = "Filter ΔP trending above 250 Pa. Replace bag filters and reset ΔP switch.",
            UnitId = filterAlarm.UnitId, AlarmId = filterAlarm.Id, Priority = WorkOrderPriority.High,
            AssignedTo = "engineer@hvacguard.local", CreatedBy = "admin@hvacguard.local",
            CreatedAt = now.AddDays(-2), DueDate = now.AddDays(-1)
        };
        wo1.Start(now.AddDays(-1).AddHours(-3));

        var wo2 = new WorkOrder
        {
            Title = "Quarterly inspection – cooling coil", Description = "Clean coil fins, check drain pan and condensate pump.",
            UnitId = units[1].Id, Priority = WorkOrderPriority.Medium,
            AssignedTo = "tech@hvacguard.local", CreatedBy = "admin@hvacguard.local",
            CreatedAt = now.AddDays(-1), DueDate = now.AddDays(6)
        };

        var wo3 = new WorkOrder
        {
            Title = "Supply fan bearing noise", Description = "Crew reported noise from supply fan. Check bearings and belt tension.",
            UnitId = units[4].Id, Priority = WorkOrderPriority.Urgent,
            AssignedTo = "engineer@hvacguard.local", CreatedBy = "engineer@hvacguard.local",
            CreatedAt = now.AddHours(-6), DueDate = now.AddDays(1)
        };

        var wo4 = new WorkOrder
        {
            Title = "Calibrate humidity sensor", Description = "RH reading deviates ~5 % from handheld reference.",
            UnitId = units[7].Id, Priority = WorkOrderPriority.Low,
            AssignedTo = "tech@hvacguard.local", CreatedBy = "admin@hvacguard.local",
            CreatedAt = now.AddDays(-10), DueDate = now.AddDays(-5)
        };
        wo4.Start(now.AddDays(-9));
        wo4.Complete("Sensor recalibrated against reference hygrometer. Deviation now < 1 %.", now.AddDays(-8));

        var wo5 = new WorkOrder
        {
            Title = "Spare parts order – V-belts", Description = "Waiting for spare V-belts to be delivered at next port call.",
            UnitId = units[2].Id, Priority = WorkOrderPriority.Medium,
            AssignedTo = "tech@hvacguard.local", CreatedBy = "engineer@hvacguard.local",
            CreatedAt = now.AddDays(-4), DueDate = now.AddDays(10)
        };
        wo5.Start(now.AddDays(-3));
        wo5.PutOnHold();

        db.WorkOrders.AddRange(wo1, wo2, wo3, wo4, wo5);
        await db.SaveChangesAsync(ct);
    }

    private static AirHandlingUnit Unit(string tag, string name, string location, double setpoint, int airflow) => new()
    {
        Tag = tag,
        Name = name,
        Location = location,
        SupplyTempSetpoint = setpoint,
        RatedAirflow = airflow,
        Manufacturer = "NordAir Marine",
        CommissionedOn = new DateTime(2021, 3, 15, 0, 0, 0, DateTimeKind.Utc),
        IsActive = true
    };
}
