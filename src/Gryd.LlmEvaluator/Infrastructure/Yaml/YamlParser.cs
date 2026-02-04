using System.Globalization;

namespace Gryd.LlmEvaluator.Infrastructure.Yaml;

public sealed class YamlParser
{
  private sealed class Context
  {
    public Context(int indent, YamlNode node)
    {
      Indent = indent;
      Node = node;
    }

    public int Indent { get; }
    public YamlNode Node { get; }
    public string? PendingKey { get; set; }
    public string? LastKey { get; set; }
  }

  public YamlNode Parse(string text)
  {
    var lines = text.Replace("\r\n", "\n").Split('\n');
    var root = new YamlMap();
    var stack = new Stack<Context>();
    stack.Push(new Context(-1, root));

    for (var i = 0; i < lines.Length; i++)
    {
      var rawLine = lines[i];
      if (string.IsNullOrWhiteSpace(rawLine))
      {
        continue;
      }

      var trimmedLine = rawLine.TrimStart(' ');
      if (trimmedLine.StartsWith("#", StringComparison.Ordinal))
      {
        continue;
      }

      var indent = rawLine.Length - trimmedLine.Length;

      while (stack.Count > 1 && indent <= stack.Peek().Indent)
      {
        stack.Pop();
      }

      var context = stack.Peek();

      if (context.Node is YamlMap map && context.PendingKey is not null)
      {
        if (trimmedLine.StartsWith("- ", StringComparison.Ordinal))
        {
          var list = new YamlList();
          map.Values[context.PendingKey] = list;
          context.LastKey = context.PendingKey;
          context.PendingKey = null;
          var listContext = new Context(Math.Max(-1, indent - 1), list);
          stack.Push(listContext);
          context = listContext;
        }
        else
        {
          var childMap = new YamlMap();
          map.Values[context.PendingKey] = childMap;
          context.LastKey = context.PendingKey;
          context.PendingKey = null;
          var mapContext = new Context(Math.Max(-1, indent - 1), childMap);
          stack.Push(mapContext);
          context = mapContext;
        }
      }

      if (trimmedLine.StartsWith("- ", StringComparison.Ordinal))
      {
        if (context.Node is not YamlList list)
        {
          if (context.Node is YamlMap mapNode && context.LastKey is not null && mapNode.Values.TryGetValue(context.LastKey, out var lastNode) && lastNode is YamlList existingList)
          {
            list = existingList;
            var listContext = new Context(indent, list);
            stack.Push(listContext);
            context = listContext;
          }
          else
          {
            throw new InvalidOperationException("List item found without list context.");
          }
        }

        var content = trimmedLine[2..].Trim();
        if (string.IsNullOrEmpty(content))
        {
          var itemMap = new YamlMap();
          list.Items.Add(itemMap);
          stack.Push(new Context(indent, itemMap));
          continue;
        }

        if (TryParseKeyValue(content, out var key, out var valuePart))
        {
          var itemMap = new YamlMap();
          list.Items.Add(itemMap);

          if (valuePart == "|")
          {
            var (block, newIndex) = ReadBlock(lines, i, indent);
            i = newIndex;
            itemMap.Values[key] = new YamlScalar(block);
            var itemContext = new Context(indent, itemMap);
            stack.Push(itemContext);
          }
          else if (string.IsNullOrWhiteSpace(valuePart))
          {
            itemMap.Values[key] = new YamlPending();
            var itemContext = new Context(indent, itemMap) { PendingKey = key };
            stack.Push(itemContext);
          }
          else
          {
            if (TryParseInlineList(valuePart, out var inlineList))
            {
              itemMap.Values[key] = inlineList;
            }
            else
            {
              itemMap.Values[key] = new YamlScalar(ParseScalar(valuePart));
            }
            var itemContext = new Context(indent, itemMap);
            stack.Push(itemContext);
          }

          continue;
        }

        list.Items.Add(new YamlScalar(ParseScalar(content)));
        continue;
      }

      if (context.Node is not YamlMap currentMap)
      {
        throw new InvalidOperationException("Mapping entry found without map context.");
      }

      if (!TryParseKeyValue(trimmedLine, out var mapKey, out var mapValue))
      {
        throw new InvalidOperationException($"Invalid YAML line: {trimmedLine}");
      }

      if (mapValue == "|")
      {
        var (block, newIndex) = ReadBlock(lines, i, indent);
        i = newIndex;
        currentMap.Values[mapKey] = new YamlScalar(block);
        context.LastKey = mapKey;
        continue;
      }

      if (string.IsNullOrWhiteSpace(mapValue))
      {
        currentMap.Values[mapKey] = new YamlPending();
        context.PendingKey = mapKey;
        context.LastKey = mapKey;
        continue;
      }

      if (TryParseInlineList(mapValue, out var listValue))
      {
        currentMap.Values[mapKey] = listValue;
      }
      else
      {
        currentMap.Values[mapKey] = new YamlScalar(ParseScalar(mapValue));
      }
      context.LastKey = mapKey;
    }

    return root;
  }

  private static bool TryParseKeyValue(string input, out string key, out string value)
  {
    var index = input.IndexOf(':');
    if (index < 0)
    {
      key = string.Empty;
      value = string.Empty;
      return false;
    }

    key = input[..index].Trim();
    value = input[(index + 1)..].Trim();
    return true;
  }

  private static (string Block, int NewIndex) ReadBlock(string[] lines, int startIndex, int baseIndent)
  {
    var blockLines = new List<string>();
    var i = startIndex + 1;
    var minIndent = int.MaxValue;

    for (; i < lines.Length; i++)
    {
      var line = lines[i];
      if (string.IsNullOrWhiteSpace(line))
      {
        blockLines.Add(string.Empty);
        continue;
      }

      var trimmed = line.TrimStart(' ');
      var indent = line.Length - trimmed.Length;
      if (indent <= baseIndent)
      {
        i--;
        break;
      }

      minIndent = Math.Min(minIndent, indent);
      blockLines.Add(line);
    }

    var normalized = blockLines
        .Select(l => string.IsNullOrWhiteSpace(l) ? string.Empty : l[minIndent..])
        .ToArray();

    return (string.Join("\n", normalized), i);
  }

  private static object? ParseScalar(string value)
  {
    value = value.Trim();
    if ((value.StartsWith('"') && value.EndsWith('"')) || (value.StartsWith('\'') && value.EndsWith('\'')))
    {
      return value[1..^1];
    }

    if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
    {
      return null;
    }

    if (bool.TryParse(value, out var boolVal))
    {
      return boolVal;
    }

    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
    {
      return intVal;
    }

    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleVal))
    {
      return doubleVal;
    }

    return value;
  }

  private static bool TryParseInlineList(string value, out YamlList list)
  {
    list = new YamlList();
    value = value.Trim();
    if (!value.StartsWith("[", StringComparison.Ordinal) || !value.EndsWith("]", StringComparison.Ordinal))
    {
      return false;
    }

    var inner = value[1..^1].Trim();
    if (string.IsNullOrWhiteSpace(inner))
    {
      return true;
    }

    var parts = inner.Split(',', StringSplitOptions.RemoveEmptyEntries);
    foreach (var part in parts)
    {
      var token = part.Trim();
      var parsed = ParseScalar(token);
      list.Items.Add(new YamlScalar(parsed));
    }

    return true;
  }
}
