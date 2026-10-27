using Microsoft.AspNetCore.Identity;

namespace HvacGuard.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
