namespace Gryd.LlmEvaluator.Domain;

public static class ScoringService
{
    public static (double Score, bool Passed) ComputeScore(IReadOnlyList<RuleResult> ruleResults)
    {
        var applicable = ruleResults.Where(r => r.Applicable).ToList();
        if (applicable.Count == 0)
        {
            return (0d, false);
        }

        var total = applicable.Sum(r => r.Weight);
        if (total <= 0)
        {
            return (0d, false);
        }

        var passedWeight = applicable.Where(r => r.Passed).Sum(r => r.Weight);
        var score = (passedWeight / total) * 100d;
        var passedAll = applicable.All(r => r.Passed);
        return (Math.Round(score, 2), passedAll);
    }
}
