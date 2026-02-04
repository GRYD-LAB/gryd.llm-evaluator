using System.Text.Json;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Application;

public sealed class RunEvaluationUseCase
{
    private readonly ILLMClient _llmClient;
    private readonly IJsonParser _jsonParser;
    private readonly int _freeModelDelayMs;

    public RunEvaluationUseCase(ILLMClient llmClient, IJsonParser jsonParser, int freeModelDelayMs)
    {
        _llmClient = llmClient;
        _jsonParser = jsonParser;
        _freeModelDelayMs = Math.Max(0, freeModelDelayMs);
    }

    public async Task<Report> ExecuteAsync(
        ModelsConfig models,
        TemplateConfig templates,
        int runs,
        int concurrency,
        string? modelFilter,
        string? templateFilter,
        CancellationToken ct)
    {
        var selectedModels = string.IsNullOrWhiteSpace(modelFilter)
            ? models.Models
            : models.Models.Where(m => string.Equals(m.Id, modelFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var selectedTemplates = string.IsNullOrWhiteSpace(templateFilter)
            ? templates.Templates
            : templates.Templates.Where(t => string.Equals(t.Id, templateFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        var modelResults = new List<ModelResult>();

        foreach (var model in selectedModels)
        {
            var templateResults = new List<TemplateResult>();

            foreach (var template in selectedTemplates)
            {
                var scenarioResults = new List<ScenarioResult>();

                foreach (var scenario in template.Scenarios)
                {
                    var mergedRules = MergeAssertions(template.Assertions, scenario.AssertionsOverride, scenario.AssertionsAdd);
                    var isFreeModel = model.Id.EndsWith(":free", StringComparison.OrdinalIgnoreCase);
                    var effectiveConcurrency = isFreeModel ? 1 : concurrency;
                    var semaphore = new SemaphoreSlim(effectiveConcurrency, effectiveConcurrency);
                    RunResult[] runResults;

                    Console.WriteLine($"Running model='{model.Id}', template='{template.Id}', scenario='{scenario.Description ?? "Scenario"}', runs={runs} (free={isFreeModel}, concurrency={effectiveConcurrency})");

                    if (isFreeModel)
                    {
                        var results = new List<RunResult>();
                        for (var i = 0; i < runs; i++)
                        {
                            Console.WriteLine($"  Run {i + 1}/{runs}");
                            results.Add(await ExecuteRunAsync(model.Id, template, scenario, mergedRules, semaphore, ct));
                        }
                        runResults = results.ToArray();
                    }
                    else
                    {
                        var runTasks = Enumerable.Range(0, runs)
                            .Select(_ => ExecuteRunAsync(model.Id, template, scenario, mergedRules, semaphore, ct))
                            .ToList();

                        runResults = await Task.WhenAll(runTasks);
                    }
                    var avgScore = runResults.Average(r => r.Score);
                    var passRate = runResults.Length == 0 ? 0d : runResults.Count(r => r.Passed) / (double)runResults.Length;

                    scenarioResults.Add(new ScenarioResult(
                        scenario.Description ?? "Scenario",
                        Math.Round(avgScore, 2),
                        Math.Round(passRate, 2),
                        runResults
                    ));
                }

                templateResults.Add(new TemplateResult(template.Id, scenarioResults));
            }

            modelResults.Add(new ModelResult(model.Id, templateResults));
        }

        var meta = new ReportMeta(DateTime.UtcNow, runs, concurrency);
        return new Report(meta, modelResults);
    }

    private async Task<RunResult> ExecuteRunAsync(
        string modelId,
        TemplateDefinition template,
        ScenarioDefinition scenario,
        IReadOnlyList<AssertionRule> rules,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        await semaphore.WaitAsync(ct);
        try
        {
            var mergedVars = MergeVars(template.Vars, scenario.Vars);
            var prompt = TemplateRenderer.Render(template.PromptTemplate, mergedVars);
            Console.WriteLine("----- Prompt Start -----");
            Console.WriteLine(prompt);
            Console.WriteLine("----- Prompt End -----");
            if (modelId.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(_freeModelDelayMs), ct);
            }
            var response = await _llmClient.ExecuteAsync(modelId, prompt, template.Llm, ct);

            if (!_jsonParser.TryParse(response, out var doc, out _))
            {
                return new RunResult(0d, false, rules.Select(r => new RuleResult(r.Path, false, r.Weight ?? 1d, true, "Invalid JSON")).ToList(), response, null);
            }

            using (doc)
            {
                var ruleResults = AssertionEngine.Evaluate(doc!.RootElement, rules);
                var (score, passed) = ScoringService.ComputeScore(ruleResults);
                var parsedJson = doc.RootElement.GetRawText();
                return new RunResult(score, passed, ruleResults, response, parsedJson);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static IReadOnlyList<AssertionRule> MergeAssertions(
        IReadOnlyList<AssertionRule> baseRules,
        IReadOnlyList<AssertionRule> overrides,
        IReadOnlyList<AssertionRule> additions)
    {
        var map = baseRules.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);

        foreach (var rule in overrides)
        {
            if (map.TryGetValue(rule.Path, out var existing))
            {
                map[rule.Path] = MergeRule(existing, rule);
            }
            else
            {
                map[rule.Path] = rule;
            }
        }

        var merged = map.Values.ToList();
        merged.AddRange(additions);
        return merged;
    }

    private static AssertionRule MergeRule(AssertionRule baseRule, AssertionRule overrideRule)
    {
        return new AssertionRule(
            baseRule.Path,
            overrideRule.Type ?? baseRule.Type,
            overrideRule.Required ?? baseRule.Required,
            overrideRule.Weight ?? baseRule.Weight,
            overrideRule.MinLen ?? baseRule.MinLen,
            overrideRule.MaxLen ?? baseRule.MaxLen,
            overrideRule.Min ?? baseRule.Min,
            overrideRule.Max ?? baseRule.Max,
            overrideRule.Enum ?? baseRule.Enum,
            overrideRule.Regex ?? baseRule.Regex,
            overrideRule.EqualsValue ?? baseRule.EqualsValue
        );
    }

    private static IReadOnlyDictionary<string, string> MergeVars(
        IReadOnlyDictionary<string, string> templateVars,
        IReadOnlyDictionary<string, string> scenarioVars)
    {
        var merged = new Dictionary<string, string>(templateVars, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in scenarioVars)
        {
            merged[key] = value;
        }

        return merged;
    }
}
