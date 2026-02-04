using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Infrastructure.Reporting;

public sealed class ConsoleReporter : IConsoleReporter
{
  public void WriteSummary(Report report)
  {
    Console.WriteLine("\nSummary\n");
    var rows = new List<(string Model, string Template, string Scenario, string AvgScore, string PassRate)>();

    foreach (var model in report.Models)
    {
      foreach (var template in model.Templates)
      {
        foreach (var scenario in template.Scenarios)
        {
          rows.Add((
              model.Id,
              template.Id,
              scenario.Description,
              scenario.AvgScore.ToString("0.00"),
              (scenario.PassRate * 100).ToString("0.00") + "%"
          ));
        }
      }
    }

    PrintTable(rows);
  }

  private static void PrintTable(IReadOnlyList<(string Model, string Template, string Scenario, string AvgScore, string PassRate)> rows)
  {
    var headers = new[] { "Model", "Template", "Scenario", "Avg Score", "Pass Rate" };
    var widths = new int[headers.Length];
    widths[0] = headers[0].Length;
    widths[1] = headers[1].Length;
    widths[2] = headers[2].Length;
    widths[3] = headers[3].Length;
    widths[4] = headers[4].Length;

    foreach (var row in rows)
    {
      widths[0] = Math.Max(widths[0], row.Model.Length);
      widths[1] = Math.Max(widths[1], row.Template.Length);
      widths[2] = Math.Max(widths[2], row.Scenario.Length);
      widths[3] = Math.Max(widths[3], row.AvgScore.Length);
      widths[4] = Math.Max(widths[4], row.PassRate.Length);
    }

    var separator = "+" + string.Join("+", widths.Select(w => new string('-', w + 2))) + "+";
    Console.WriteLine(separator);
    Console.WriteLine("| " + headers[0].PadRight(widths[0]) + " | " + headers[1].PadRight(widths[1]) + " | " + headers[2].PadRight(widths[2]) + " | " + headers[3].PadRight(widths[3]) + " | " + headers[4].PadRight(widths[4]) + " |");
    Console.WriteLine(separator);

    foreach (var row in rows)
    {
      Console.WriteLine("| " + row.Model.PadRight(widths[0]) + " | " + row.Template.PadRight(widths[1]) + " | " + row.Scenario.PadRight(widths[2]) + " | " + row.AvgScore.PadRight(widths[3]) + " | " + row.PassRate.PadRight(widths[4]) + " |");
    }

    Console.WriteLine(separator);
  }
}
