using HvacGuard.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HvacGuard.Web.Filters;

/// <summary>
/// Translates domain exceptions into HTTP responses so controllers stay free of try/catch blocks:
/// <list type="bullet">
///   <item><see cref="NotFoundException"/> → 404</item>
///   <item><see cref="DomainException"/> (business rule violation) → 400 for the API,
///         or a redirect back with an error message for MVC pages.</item>
/// </list>
/// </summary>
public sealed class DomainExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var isApi = context.HttpContext.Request.Path.StartsWithSegments("/api");

        switch (context.Exception)
        {
            case NotFoundException notFound:
                context.Result = isApi
                    ? new NotFoundObjectResult(new ProblemDetails { Title = "Not found", Detail = notFound.Message, Status = 404 })
                    : new NotFoundResult();
                context.ExceptionHandled = true;
                break;

            case DomainException domain:
                if (isApi)
                {
                    context.Result = new BadRequestObjectResult(
                        new ProblemDetails { Title = "Business rule violation", Detail = domain.Message, Status = 400 });
                }
                else
                {
                    var tempData = tempDataFactory.GetTempData(context.HttpContext);
                    tempData["Error"] = domain.Message;
                    context.Result = new RedirectResult(GetLocalReferer(context.HttpContext));
                }

                context.ExceptionHandled = true;
                break;
        }
    }

    private static string GetLocalReferer(HttpContext httpContext)
    {
        var referer = httpContext.Request.Headers.Referer.ToString();
        return Uri.TryCreate(referer, UriKind.Absolute, out var uri)
               && string.Equals(uri.Host, httpContext.Request.Host.Host, StringComparison.OrdinalIgnoreCase)
            ? uri.PathAndQuery
            : "/";
    }
}
