using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HvacGuard.Web.Filters;

/// <summary>
/// Protects machine-to-machine endpoints (e.g. a SCADA / PLC gateway pushing telemetry) with an API key header.
/// </summary>
public sealed class ApiKeyFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public const string HeaderName = "X-Api-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = configuration["Telemetry:ApiKey"];
        if (string.IsNullOrEmpty(expected)
            || !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(provided.ToString())))
        {
            context.Result = new UnauthorizedObjectResult(new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = $"A valid '{HeaderName}' header is required.",
                Status = StatusCodes.Status401Unauthorized
            });
        }
    }
}
