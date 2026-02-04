using System.Text.Json;
using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Infrastructure.Reporting;

public sealed class JsonReportWriter : IReportWriter
{
  public Task WriteAsync(Report report, string outputPath, CancellationToken ct)
  {
    var directory = Path.GetDirectoryName(outputPath);
    if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }

    var options = new JsonSerializerOptions
    {
      WriteIndented = true
    };

    var json = JsonSerializer.Serialize(report, options);
    return File.WriteAllTextAsync(outputPath, json, ct);
  }
}
