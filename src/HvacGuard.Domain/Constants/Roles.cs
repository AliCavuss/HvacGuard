namespace HvacGuard.Domain.Constants;

public static class Roles
{
    /// <summary>Manages vessels, units, alarm rules and users.</summary>
    public const string Admin = "Admin";

    /// <summary>Acknowledges alarms and works on maintenance work orders.</summary>
    public const string Engineer = "Engineer";

    /// <summary>Read-only access (e.g. ship owner representatives).</summary>
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Engineer, Viewer];
}
