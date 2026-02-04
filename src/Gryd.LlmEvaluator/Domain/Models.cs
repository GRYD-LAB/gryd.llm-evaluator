namespace Gryd.LlmEvaluator.Domain;

public sealed record AppConfig(
    string ModelsFile,
    string TemplatesFile,
    int Runs,
    int Concurrency,
    int FreeModelDelayMs,
    RetryConfig Retry,
    OutputConfig Output
);

public sealed record RetryConfig(int MaxAttempts, int BackoffMs);

public sealed record OutputConfig(string DefaultPath);

public sealed record ModelsConfig(IReadOnlyList<ModelDefinition> Models);

public sealed record ModelDefinition(string Id);

public sealed record TemplateConfig(IReadOnlyList<TemplateDefinition> Templates);

public sealed record TemplateDefinition(
    string Id,
    string? Description,
    string PromptTemplate,
    IReadOnlyDictionary<string, string> Vars,
    LlmParams Llm,
    IReadOnlyList<AssertionRule> Assertions,
    IReadOnlyList<ScenarioDefinition> Scenarios
);

public sealed record LlmParams(double Temperature, double TopP, int MaxTokens);

public sealed record ScenarioDefinition(
    string? Description,
    IReadOnlyDictionary<string, string> Vars,
    IReadOnlyList<AssertionRule> AssertionsAdd,
    IReadOnlyList<AssertionRule> AssertionsOverride
);

public sealed record AssertionRule(
    string Path,
    string? Type,
    bool? Required,
    double? Weight,
    int? MinLen,
    int? MaxLen,
    double? Min,
    double? Max,
    IReadOnlyList<string>? Enum,
    string? Regex,
    ScalarValue? EqualsValue
);

public enum ScalarType
{
    String,
    Number,
    Boolean,
    Null
}

public sealed record ScalarValue(ScalarType Type, object? Value);

public sealed record RuleResult(
    string Path,
    bool Passed,
    double Weight,
    bool Applicable,
    string Message
);

public sealed record RunResult(
    double Score,
    bool Passed,
    IReadOnlyList<RuleResult> RuleResults,
    string RawResponse,
    string? ParsedJson
);

public sealed record ScenarioResult(
    string Description,
    double AvgScore,
    double PassRate,
    IReadOnlyList<RunResult> Runs
);

public sealed record TemplateResult(
    string Id,
    IReadOnlyList<ScenarioResult> Scenarios
);

public sealed record ModelResult(
    string Id,
    IReadOnlyList<TemplateResult> Templates
);

public sealed record ReportMeta(
    DateTime GeneratedAt,
    int Runs,
    int Concurrency
);

public sealed record Report(
    ReportMeta Meta,
    IReadOnlyList<ModelResult> Models
);
