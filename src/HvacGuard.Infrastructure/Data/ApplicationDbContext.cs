using HvacGuard.Domain.Entities;
using HvacGuard.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HvacGuard.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Vessel> Vessels => Set<Vessel>();
    public DbSet<AirHandlingUnit> Units => Set<AirHandlingUnit>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();
    public DbSet<AlarmRule> AlarmRules => Set<AlarmRule>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
