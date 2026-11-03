using System.Text.RegularExpressions;
using PayableServices.Domain.Entities;

namespace PayableServices.Application.Models;

public static class ReferenceValidation
{
    public static bool ValidDefinitions(IReadOnlyCollection<ReferenceFieldDefinition> definitions)
    {
        if (definitions.Count == 0 || definitions.Select(x => x.FieldKey).Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            return false;
        foreach (var field in definitions)
        {
            if (string.IsNullOrWhiteSpace(field.FieldKey) || field.FieldKey.Length > 100 ||
                string.IsNullOrWhiteSpace(field.DisplayLabel) || field.DisplayLabel.Length > 200 ||
                field.DataType is not ("string" or "digits") ||
                field.MinLength is < 0 or > 1000 || field.MaxLength is null or <= 0 or > 1000 ||
                field.MinLength > field.MaxLength || field.ValidationRule?.Length > 500)
                return false;
            try
            {
                if (!string.IsNullOrEmpty(field.ValidationRule))
                    _ = new Regex(field.ValidationRule, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
            }
            catch (ArgumentException) { return false; }
        }
        return true;
    }

    public static bool TryNormalize(IReadOnlyCollection<ReferenceFieldDefinition> definitions,
        IReadOnlyCollection<ReferenceValue> values, out SortedDictionary<string, string> normalized)
    {
        normalized = new(StringComparer.Ordinal);
        if (!ValidDefinitions(definitions) || values.Count > definitions.Count) return false;
        foreach (var value in values)
        {
            if (value is null || value.Key is null || value.Value is null || !normalized.TryAdd(value.Key, value.Value.Trim()))
                return false;
        }
        if (normalized.Keys.Any(key => definitions.All(d => d.FieldKey != key))) return false;
        foreach (var field in definitions)
        {
            normalized.TryGetValue(field.FieldKey, out var value);
            if (string.IsNullOrEmpty(value))
            {
                if (field.IsRequired) return false;
                continue;
            }
            if (value.Length < (field.MinLength ?? 0) || value.Length > field.MaxLength ||
                (field.DataType == "digits" && value.Any(c => c is < '0' or > '9'))) return false;
            try
            {
                if (!string.IsNullOrEmpty(field.ValidationRule) &&
                    !Regex.IsMatch(value, field.ValidationRule, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                    return false;
            }
            catch (RegexMatchTimeoutException) { return false; }
        }
        return true;
    }
}
