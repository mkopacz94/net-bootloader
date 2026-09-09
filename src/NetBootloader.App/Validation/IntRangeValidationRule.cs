using System.Globalization;
using System.Windows.Controls;
using NetBootloader.App.Localization;

namespace NetBootloader.App.Validation;

/// <summary>
/// Rejects a bound value unless it's a plain whole number (no sign, decimal point, or
/// thousands separator) within [<see cref="Min"/>, <see cref="Max"/>]. Intended for use
/// with <see cref="ValidationStep.RawProposedValue"/> so it sees exactly what the user
/// typed, before type conversion.
/// </summary>
public sealed class IntRangeValidationRule : ValidationRule
{
    public int Min { get; set; }

    public int Max { get; set; }

    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        var text = value as string ?? value?.ToString() ?? string.Empty;

        if (!int.TryParse(text, NumberStyles.None, cultureInfo, out var parsed) || parsed < Min || parsed > Max)
        {
            return new ValidationResult(false, Strings.Instance.ValidationRangeError(Min, Max));
        }

        return ValidationResult.ValidResult;
    }
}
