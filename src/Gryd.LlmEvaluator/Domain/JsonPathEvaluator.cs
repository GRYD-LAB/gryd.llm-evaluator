using System.Text.Json;

namespace Gryd.LlmEvaluator.Domain;

public static class JsonPathEvaluator
{
  public static bool TryEvaluate(JsonElement root, string path, out JsonElement value)
  {
    value = root;
    if (string.IsNullOrWhiteSpace(path))
    {
      return false;
    }

    path = path.Trim();
    if (!path.StartsWith("$", StringComparison.Ordinal))
    {
      return false;
    }

    var index = 1;
    while (index < path.Length)
    {
      if (path[index] == '.')
      {
        index++;
        var start = index;
        while (index < path.Length && path[index] != '.' && path[index] != '[')
        {
          index++;
        }

        var prop = path[start..index];
        if (string.IsNullOrWhiteSpace(prop))
        {
          return false;
        }

        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(prop, out value))
        {
          return false;
        }

        continue;
      }

      if (path[index] == '[')
      {
        index++;
        var start = index;
        while (index < path.Length && path[index] != ']')
        {
          index++;
        }

        if (index >= path.Length || path[index] != ']')
        {
          return false;
        }

        var token = path[start..index];
        if (!int.TryParse(token, out var arrIndex))
        {
          return false;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
          return false;
        }

        var length = value.GetArrayLength();
        if (arrIndex < 0 || arrIndex >= length)
        {
          return false;
        }

        value = value.EnumerateArray().ElementAt(arrIndex);
        index++;
        continue;
      }

      return false;
    }

    return true;
  }
}
