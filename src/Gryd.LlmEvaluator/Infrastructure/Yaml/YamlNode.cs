namespace Gryd.LlmEvaluator.Infrastructure.Yaml;

public abstract class YamlNode
{
}

public sealed class YamlScalar : YamlNode
{
    public YamlScalar(object? value) => Value = value;
    public object? Value { get; }
}

public sealed class YamlMap : YamlNode
{
    public Dictionary<string, YamlNode> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class YamlList : YamlNode
{
    public List<YamlNode> Items { get; } = new();
}

public sealed class YamlPending : YamlNode
{
}
