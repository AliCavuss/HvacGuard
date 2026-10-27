using HvacGuard.Infrastructure.Data;
using HvacGuard.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HvacGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IVesselService, VesselService>();
        services.AddScoped<IUnitService, UnitService>();
        services.AddScoped<ITelemetryService, TelemetryService>();
        services.AddScoped<IAlarmService, AlarmService>();
        services.AddScoped<IAlarmRuleService, AlarmRuleService>();
        services.AddScoped<IWorkOrderService, WorkOrderService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
