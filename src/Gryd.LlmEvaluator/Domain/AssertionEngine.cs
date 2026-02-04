using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gryd.LlmEvaluator.Domain;

public static class AssertionEngine
{
  public static IReadOnlyList<RuleResult> Evaluate(JsonElement root, IReadOnlyList<AssertionRule> rules)
  {
    var results = new List<RuleResult>(rules.Count);

    foreach (var rule in rules)
    {
      var required = rule.Required ?? true;
      var weight = rule.Weight ?? 1d;

      if (!JsonPathEvaluator.TryEvaluate(root, rule.Path, out var value))
      {
        if (required)
        {
          results.Add(new RuleResult(rule.Path, false, weight, true, "Path not found"));
        }
        else
        {
          results.Add(new RuleResult(rule.Path, true, weight, false, "Optional path not found"));
        }
        continue;
      }

      var errors = new List<string>();

      if (!ValidateType(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      if (!ValidateStringLength(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      if (!ValidateNumberRange(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      if (!ValidateEnum(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      if (!ValidateRegex(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      if (!ValidateEquals(rule, value, errors))
      {
        results.Add(new RuleResult(rule.Path, false, weight, true, string.Join("; ", errors)));
        continue;
      }

      results.Add(new RuleResult(rule.Path, true, weight, true, "OK"));
    }

    return results;
  }

  private static bool ValidateType(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (string.IsNullOrWhiteSpace(rule.Type))
    {
      return true;
    }

    var expected = rule.Type.Trim().ToLowerInvariant();
    var kind = value.ValueKind;

    var matches = expected switch
    {
      "string" => kind == JsonValueKind.String,
      "integer" => kind == JsonValueKind.Number && value.TryGetInt64(out _),
      "number" => kind == JsonValueKind.Number,
      "boolean" => kind == JsonValueKind.True || kind == JsonValueKind.False,
      "object" => kind == JsonValueKind.Object,
      "array" => kind == JsonValueKind.Array,
      _ => false
    };

    if (!matches)
    {
      errors.Add($"Expected type {expected}");
    }

    return matches;
  }

  private static bool ValidateStringLength(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (value.ValueKind != JsonValueKind.String)
    {
      return true;
    }

    var length = value.GetString()?.Length ?? 0;
    if (rule.MinLen.HasValue && length < rule.MinLen.Value)
    {
      errors.Add($"Min length {rule.MinLen} not met");
    }
    if (rule.MaxLen.HasValue && length > rule.MaxLen.Value)
    {
      errors.Add($"Max length {rule.MaxLen} exceeded");
    }

    return errors.Count == 0;
  }

  private static bool ValidateNumberRange(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (value.ValueKind != JsonValueKind.Number)
    {
      return true;
    }

    var num = value.GetDouble();
    if (rule.Min.HasValue && num < rule.Min.Value)
    {
      errors.Add($"Min {rule.Min} not met");
    }
    if (rule.Max.HasValue && num > rule.Max.Value)
    {
      errors.Add($"Max {rule.Max} exceeded");
    }

    return errors.Count == 0;
  }

  private static bool ValidateEnum(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (rule.Enum == null || rule.Enum.Count == 0)
    {
      return true;
    }

    var str = value.ValueKind switch
    {
      JsonValueKind.String => value.GetString() ?? string.Empty,
      JsonValueKind.Number => value.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
      JsonValueKind.True => "true",
      JsonValueKind.False => "false",
      _ => string.Empty
    };

    var match = rule.Enum.Contains(str, StringComparer.OrdinalIgnoreCase);
    if (!match)
    {
      errors.Add("Value not in enum set");
    }

    return match;
  }

  private static bool ValidateRegex(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (string.IsNullOrWhiteSpace(rule.Regex))
    {
      return true;
    }

    if (value.ValueKind != JsonValueKind.String)
    {
      errors.Add("Regex can only be applied to string");
      return false;
    }

    var regex = new Regex(rule.Regex);
    var text = value.GetString() ?? string.Empty;
    var match = regex.IsMatch(text);
    if (!match)
    {
      errors.Add("Regex did not match");
    }

    return match;
  }

  private static bool ValidateEquals(AssertionRule rule, JsonElement value, List<string> errors)
  {
    if (rule.EqualsValue is null)
    {
      return true;
    }

    var ok = rule.EqualsValue.Type switch
    {
      ScalarType.String => value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), rule.EqualsValue.Value?.ToString(), StringComparison.Ordinal),
      ScalarType.Number => value.ValueKind == JsonValueKind.Number && Math.Abs(value.GetDouble() - Convert.ToDouble(rule.EqualsValue.Value)) < 0.000001,
      ScalarType.Boolean => (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) && (value.GetBoolean() == Convert.ToBoolean(rule.EqualsValue.Value)),
      ScalarType.Null => value.ValueKind == JsonValueKind.Null,
      _ => false
    };

    if (!ok)
    {
      errors.Add("Equals check failed");
    }

    return ok;
  }
}
