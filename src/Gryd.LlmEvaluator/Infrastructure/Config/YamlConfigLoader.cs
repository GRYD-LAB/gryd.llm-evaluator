using System.Globalization;
using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;
using Gryd.LlmEvaluator.Infrastructure.Yaml;

namespace Gryd.LlmEvaluator.Infrastructure.Config;

public sealed class YamlConfigLoader : IConfigLoader
{
    private readonly YamlParser _parser = new();

    public Task<(AppConfig App, ModelsConfig Models, TemplateConfig Templates)> LoadAsync(string configPath, CancellationToken ct)
    {
        var configRoot = LoadYaml(configPath);

        var modelsFile = GetString(configRoot, "models_file") ?? "models.yml";
        var templatesFile = GetString(configRoot, "templates_file");
        var templatesDir = GetString(configRoot, "templates_dir");
        var runs = GetInt(configRoot, "runs") ?? 3;
        var concurrency = GetInt(configRoot, "concurrency") ?? 3;
        var retryNode = GetMap(configRoot, "retry");
        var outputNode = GetMap(configRoot, "output");
        var freeModelDelayMs = GetInt(configRoot, "free_model_delay_ms") ?? 1000;

        var retry = new RetryConfig(
            GetInt(retryNode, "max_attempts") ?? 2,
            GetInt(retryNode, "backoff_ms") ?? 500
        );

        var output = new OutputConfig(
            GetString(outputNode, "default_path") ?? "./reports/report.json"
        );

        var templateSource = templatesFile ?? templatesDir ?? "templates.yml";
        var appConfig = new AppConfig(modelsFile, templateSource, runs, concurrency, freeModelDelayMs, retry, output);

        var modelsRoot = LoadYaml(modelsFile);
        var models = ParseModels(modelsRoot);

        var templates = new List<TemplateDefinition>();
        if (!string.IsNullOrWhiteSpace(templatesDir))
        {
            templates.AddRange(LoadTemplatesFromDirectory(templatesDir));
        }
        else
        {
            var filePath = templatesFile ?? "templates.yml";
            var templatesRoot = LoadYaml(filePath);
            templates.AddRange(ParseTemplates(templatesRoot));
        }

        return Task.FromResult((appConfig, models, new TemplateConfig(templates)));
    }

    private YamlMap LoadYaml(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"YAML file not found: {path}");
        }

        var text = File.ReadAllText(path);
        var node = _parser.Parse(text) as YamlMap;
        if (node is null)
        {
            throw new InvalidOperationException($"Invalid YAML structure in {path}");
        }

        return node;
    }

    private static ModelsConfig ParseModels(YamlMap root)
    {
        var list = GetList(root, "models");
        var models = list?.Items
            .OfType<YamlMap>()
            .Select(m => new ModelDefinition(GetString(m, "id") ?? throw new InvalidOperationException("Model id missing")))
            .ToList() ?? new List<ModelDefinition>();

        return new ModelsConfig(models);
    }

    private IEnumerable<TemplateDefinition> LoadTemplatesFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Templates directory not found: {directory}");
        }

        var templates = new List<TemplateDefinition>();
        var files = Directory.GetFiles(directory, "*.yml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var root = LoadYaml(file);
            templates.AddRange(ParseTemplates(root));
        }

        return templates;
    }

    private static IEnumerable<TemplateDefinition> ParseTemplates(YamlMap root)
    {
        var list = GetList(root, "templates");
        var templates = new List<TemplateDefinition>();

        if (list != null)
        {
            foreach (var item in list.Items.OfType<YamlMap>())
            {
                var id = GetString(item, "id") ?? throw new InvalidOperationException("Template id missing");
                var description = GetString(item, "description");
                var prompt = GetString(item, "prompt_template") ?? string.Empty;
                var vars = ParseVars(GetMap(item, "vars"));
                var llm = ParseLlmParams(GetMap(item, "llm"));
                var assertions = ParseAssertions(GetList(item, "assertions"));
                var scenarios = ParseScenarios(GetList(item, "scenarios"));
                templates.Add(new TemplateDefinition(id, description, prompt, vars, llm, assertions, scenarios));
            }
        }

        return templates;
    }

    private static LlmParams ParseLlmParams(YamlMap? map)
    {
        if (map is null)
        {
            return new LlmParams(0.2, 1.0, 1024);
        }

        var temperature = GetDouble(map, "temperature") ?? 0.2;
        var topP = GetDouble(map, "top_p") ?? 1.0;
        var maxTokens = GetInt(map, "max_tokens") ?? 1024;
        return new LlmParams(temperature, topP, maxTokens);
    }

    private static IReadOnlyList<ScenarioDefinition> ParseScenarios(YamlList? list)
    {
        var scenarios = new List<ScenarioDefinition>();
        if (list == null)
        {
            return scenarios;
        }

        foreach (var item in list.Items.OfType<YamlMap>())
        {
            var description = GetString(item, "description") ?? GetString(item, "id");
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new InvalidOperationException("Scenario description missing");
            }
            var vars = ParseVars(GetMap(item, "vars"));
            var add = ParseAssertions(GetList(item, "assertions_add"));
            var overrides = ParseAssertions(GetList(item, "assertions_override"));
            scenarios.Add(new ScenarioDefinition(description, vars, add, overrides));
        }

        return scenarios;
    }

    private static IReadOnlyDictionary<string, string> ParseVars(YamlMap? map)
    {
        var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (map == null)
        {
            return vars;
        }

        foreach (var (key, node) in map.Values)
        {
            if (node is YamlScalar scalar)
            {
                vars[key] = scalar.Value?.ToString() ?? string.Empty;
            }
        }

        return vars;
    }

    private static IReadOnlyList<AssertionRule> ParseAssertions(YamlList? list)
    {
        var rules = new List<AssertionRule>();
        if (list == null)
        {
            return rules;
        }

        foreach (var node in list.Items.OfType<YamlMap>())
        {
            var path = GetString(node, "path") ?? throw new InvalidOperationException("Assertion path missing");
            var rule = new AssertionRule(
                path,
                GetString(node, "type"),
                GetBool(node, "required"),
                GetDouble(node, "weight"),
                GetInt(node, "min_len"),
                GetInt(node, "max_len"),
                GetDouble(node, "min"),
                GetDouble(node, "max"),
                GetStringList(node, "enum"),
                GetString(node, "regex"),
                GetScalar(node, "equals")
            );

            rules.Add(rule);
        }

        return rules;
    }

    private static ScalarValue? GetScalar(YamlMap map, string key)
    {
        if (!map.Values.TryGetValue(key, out var node))
        {
            return null;
        }

        if (node is not YamlScalar scalar)
        {
            return null;
        }

        return scalar.Value switch
        {
            null => new ScalarValue(ScalarType.Null, null),
            bool b => new ScalarValue(ScalarType.Boolean, b),
            int i => new ScalarValue(ScalarType.Number, i),
            double d => new ScalarValue(ScalarType.Number, d),
            _ => new ScalarValue(ScalarType.String, scalar.Value?.ToString())
        };
    }

    private static YamlMap? GetMap(YamlMap map, string key)
    {
        return map.Values.TryGetValue(key, out var node) ? node as YamlMap : null;
    }

    private static YamlList? GetList(YamlMap map, string key)
    {
        return map.Values.TryGetValue(key, out var node) ? node as YamlList : null;
    }

    private static string? GetString(YamlMap? map, string key)
    {
        if (map != null && map.Values.TryGetValue(key, out var node) && node is YamlScalar scalar)
        {
            return scalar.Value?.ToString();
        }
        return null;
    }

    private static int? GetInt(YamlMap? map, string key)
    {
        if (map == null)
        {
            return null;
        }

        if (map.Values.TryGetValue(key, out var node) && node is YamlScalar scalar)
        {
            if (scalar.Value is int i)
            {
                return i;
            }

            if (scalar.Value is double d)
            {
                return (int)d;
            }

            if (int.TryParse(scalar.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static double? GetDouble(YamlMap? map, string key)
    {
        if (map == null)
        {
            return null;
        }

        if (map.Values.TryGetValue(key, out var node) && node is YamlScalar scalar)
        {
            if (scalar.Value is double d)
            {
                return d;
            }

            if (scalar.Value is int i)
            {
                return i;
            }

            if (double.TryParse(scalar.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }
        return null;
    }

    private static bool? GetBool(YamlMap map, string key)
    {
        if (map.Values.TryGetValue(key, out var node) && node is YamlScalar scalar)
        {
            if (scalar.Value is bool b)
            {
                return b;
            }

            if (bool.TryParse(scalar.Value?.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? GetStringList(YamlMap map, string key)
    {
        if (!map.Values.TryGetValue(key, out var node) || node is not YamlList list)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var item in list.Items)
        {
            if (item is YamlScalar scalar)
            {
                values.Add(scalar.Value?.ToString() ?? string.Empty);
            }
        }

        return values;
    }
}
