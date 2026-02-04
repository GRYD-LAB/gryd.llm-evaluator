using System.Text.Json;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Application;

public interface ILLMClient
{
  Task<string> ExecuteAsync(string modelId, string prompt, LlmParams llm, CancellationToken ct);
}

public interface IConfigLoader
{
  Task<(AppConfig App, ModelsConfig Models, TemplateConfig Templates)> LoadAsync(string configPath, CancellationToken ct);
}

public interface IReportWriter
{
  Task WriteAsync(Report report, string outputPath, CancellationToken ct);
}

public interface IHtmlReportWriter
{
  Task WriteAsync(Report report, string outputPath, CancellationToken ct);
}

public interface IConsoleReporter
{
  void WriteSummary(Report report);
}

public interface IJsonParser
{
  bool TryParse(string json, out JsonDocument? document, out string? error);
}
