using System.ComponentModel.DataAnnotations;
using HvacGuard.Domain.Common;

namespace HvacGuard.Web.Validation;

/// <summary>Validates an IMO number including its check digit.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class ValidImoAttribute : ValidationAttribute
{
    public ValidImoAttribute()
        : base("{0} must be a valid 7-digit IMO number (the check digit is verified).")
    {
    }

    public override bool IsValid(object? value) =>
        value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || ImoNumberValidator.IsValid(s)));
}
