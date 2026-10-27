namespace HvacGuard.Domain.Common;

/// <summary>
/// Validates IMO ship identification numbers (7 digits, the last one being a check digit).
/// The check digit is the last digit of the sum of the first six digits multiplied by 7, 6, 5, 4, 3 and 2.
/// Example: IMO 9074729 → 9×7 + 0×6 + 7×5 + 4×4 + 7×3 + 2×2 = 139 → check digit 9.
/// </summary>
public static class ImoNumberValidator
{
    public static bool IsValid(string? value)
    {
        var digits = Normalize(value);
        if (digits.Length != 7 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 6; i++)
        {
            sum += (digits[i] - '0') * (7 - i);
        }

        return sum % 10 == digits[6] - '0';
    }

    /// <summary>Removes an optional "IMO" prefix and whitespace, e.g. "IMO 9074729" → "9074729".</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("IMO", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[3..];
        }

        return new string(trimmed.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
}
