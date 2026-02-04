using System.Text.Json;
using Gryd.LlmEvaluator.Domain;
using Xunit;

namespace Gryd.LlmEvaluator.Tests;

public sealed class AssertionEngineTests
{
    [Fact]
    public void Evaluate_ValidatesTypesRangesEnumsAndRegex()
    {
        var json = "{\"name\":\"Maria\",\"age\":25,\"verdict\":\"yes\"}";
        using var doc = JsonDocument.Parse(json);

        var rules = new List<AssertionRule>
        {
            new("$.name", "string", true, 3, 2, null, null, null, null, null, null, null),
            new("$.age", "integer", true, 2, null, null, 18, 30, null, null, null),
            new("$.verdict", "string", true, 5, null, null, null, null, new List<string> { "yes", "no" }, "^yes$", null)
        };

        var results = AssertionEngine.Evaluate(doc.RootElement, rules);
        Assert.All(results, r => Assert.True(r.Passed));

        var (score, passed) = ScoringService.ComputeScore(results);
        Assert.True(passed);
        Assert.Equal(100, score);
    }
}
