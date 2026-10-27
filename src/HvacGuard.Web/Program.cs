using System.Globalization;
using HvacGuard.Infrastructure;
using HvacGuard.Infrastructure.Data;
using HvacGuard.Infrastructure.Identity;
using HvacGuard.Web.Authorization;
using HvacGuard.Web.Filters;
using HvacGuard.Web.Hubs;
using HvacGuard.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;

// Engineering values (e.g. 18.5 °C) must always use '.' as decimal separator, regardless of the server locale.
var culture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

// ---------- Infrastructure (EF Core + SQL Server, application services) ----------
builder.Services.AddInfrastructure(builder.Configuration);

// ---------- Identity & authorization ----------
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanManageAssets, policy => policy.RequireRole(HvacGuard.Domain.Constants.Roles.Admin))
    .AddPolicy(Policies.CanOperate, policy => policy.RequireRole(
        HvacGuard.Domain.Constants.Roles.Admin, HvacGuard.Domain.Constants.Roles.Engineer));

// ---------- MVC ----------
builder.Services.AddControllersWithViews(options =>
{
    // Every page requires a signed-in user unless explicitly marked [AllowAnonymous].
    var requireAuthenticatedUser = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(requireAuthenticatedUser));
    options.Filters.Add<DomainExceptionFilter>();
});

builder.Services.AddScoped<ApiKeyFilter>();
builder.Services.AddScoped<SelectListService>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("database");

// ---------- Real-time (SignalR) + telemetry simulator ----------
builder.Services.AddSignalR();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));
builder.Services.AddHostedService<TelemetrySimulator>();

var app = builder.Build();

await app.Services.InitialiseDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization("en-US");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapHub<TelemetryHub>("/hubs/telemetry");
app.MapHealthChecks("/health");

app.Run();

// Exposed for integration tests (WebApplicationFactory<Program>).
public partial class Program { }
