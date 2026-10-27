using System.Security.Claims;
using HvacGuard.Domain.Constants;

namespace HvacGuard.Web.Authorization;

public static class Policies
{
    /// <summary>Create / edit / delete vessels, units and alarm rules.</summary>
    public const string CanManageAssets = nameof(CanManageAssets);

    /// <summary>Acknowledge alarms and work on work orders.</summary>
    public const string CanOperate = nameof(CanOperate);
}

public static class ClaimsPrincipalExtensions
{
    public static bool CanManageAssets(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);

    public static bool CanOperate(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Engineer);

    public static string UserName(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? throw new InvalidOperationException("User is not authenticated.");
}
