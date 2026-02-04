using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;
using Gryd.LlmEvaluator.Infrastructure.Config;
using Gryd.LlmEvaluator.Infrastructure.Http;
using Gryd.LlmEvaluator.Infrastructure.Json;
using Gryd.LlmEvaluator.Infrastructure.Reporting;

namespace Gryd.LlmEvaluator;

public static class Program
{
  public static async Task<int> Main(string[] args)
  {
    var options = CliOptions.Parse(args);
    if (options.ShowHelp)
    {
      CliOptions.PrintHelp();
      return 0;
    }

    try
    {
      var loader = new YamlConfigLoader();
      var (appConfig, models, templates) = await loader.LoadAsync(options.ConfigPath, CancellationToken.None);

      var runs = options.Runs ?? appConfig.Runs;
      var concurrency = options.Concurrency ?? appConfig.Concurrency;
      var outputPath = options.OutPath ?? PathWithTimestamp.AppendTimestamp(appConfig.Output.DefaultPath);

      var apiKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
      if (string.IsNullOrWhiteSpace(apiKey))
      {
        Console.Error.WriteLine("OPENROUTER_API_KEY is not set.");
        return 1;
      }

      using var httpClient = new HttpClient();
      httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

      var llmClient = new OpenRouterClient(httpClient, appConfig.Retry);
      var jsonParser = new JsonParser();
      var useCase = new RunEvaluationUseCase(llmClient, jsonParser, appConfig.FreeModelDelayMs);

      var report = await useCase.ExecuteAsync(
          models,
          templates,
          runs,
          concurrency,
          options.ModelFilter,
          options.TemplateFilter,
          CancellationToken.None);

      var writer = new JsonReportWriter();
      await writer.WriteAsync(report, outputPath, CancellationToken.None);

      var htmlWriter = new HtmlReportWriter();
      var htmlPath = Path.ChangeExtension(outputPath, ".html");
      await htmlWriter.WriteAsync(report, htmlPath, CancellationToken.None);

      var consoleReporter = new ConsoleReporter();
      consoleReporter.WriteSummary(report);

      Console.WriteLine($"\nReport written to: {outputPath}");
      Console.WriteLine($"HTML report written to: {htmlPath}");
      return 0;
    }
    catch (Exception ex)
    {
      Console.Error.WriteLine($"Error: {ex.Message}");
      return 1;
    }
  }
}

internal static class PathWithTimestamp
{
  public static string AppendTimestamp(string path)
  {
    var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
    var directory = Path.GetDirectoryName(path);
    var fileName = Path.GetFileNameWithoutExtension(path);
    var extension = Path.GetExtension(path);

    if (string.IsNullOrWhiteSpace(fileName))
    {
      fileName = "report";
    }

    var newFileName = $"{fileName}_{timestamp}{extension}";
    return string.IsNullOrWhiteSpace(directory) ? newFileName : Path.Combine(directory, newFileName);
  }
}

internal sealed class CliOptions
{
  public string ConfigPath { get; private set; } = "./config.yml";
  public string? OutPath { get; private set; }
  public int? Runs { get; private set; }
  public int? Concurrency { get; private set; }
  public string? TemplateFilter { get; private set; }
  public string? ModelFilter { get; private set; }
  public bool ShowHelp { get; private set; }

  public static CliOptions Parse(string[] args)
  {
    var options = new CliOptions();
    for (var i = 0; i < args.Length; i++)
    {
      var arg = args[i];
      if (arg is "--help" or "-h")
      {
        options.ShowHelp = true;
        continue;
      }

      if (TryGetValue(arg, "--config", out var value) || TryGetNext(args, ref i, "--config", out value))
      {
        options.ConfigPath = value;
        continue;
      }

      if (TryGetValue(arg, "--out", out value) || TryGetNext(args, ref i, "--out", out value))
      {
        options.OutPath = value;
        continue;
      }

      if (TryGetValue(arg, "--runs", out value) || TryGetNext(args, ref i, "--runs", out value))
      {
        options.Runs = int.Parse(value);
        continue;
      }

      if (TryGetValue(arg, "--concurrency", out value) || TryGetNext(args, ref i, "--concurrency", out value))
      {
        options.Concurrency = int.Parse(value);
        continue;
      }

      if (TryGetValue(arg, "--template", out value) || TryGetNext(args, ref i, "--template", out value))
      {
        options.TemplateFilter = value;
        continue;
      }

      if (TryGetValue(arg, "--model", out value) || TryGetNext(args, ref i, "--model", out value))
      {
        options.ModelFilter = value;
        continue;
      }

      throw new ArgumentException($"Unknown argument: {arg}");
    }

    return options;
  }

  public static void PrintHelp()
  {
    Console.WriteLine("Gryd.LlmEvaluator");
    Console.WriteLine("\nUsage:");
    Console.WriteLine("  dotnet run --project src/Gryd.LlmEvaluator -- [options]");
    Console.WriteLine("\nOptions:");
    Console.WriteLine("  --config <path>       Path to config.yml (default: ./config.yml)");
    Console.WriteLine("  --out <path>          Output report path");
    Console.WriteLine("  --runs <int>          Override runs per scenario");
    Console.WriteLine("  --concurrency <int>   Override concurrency limit");
    Console.WriteLine("  --template <id>       Filter by template id");
    Console.WriteLine("  --model <id>          Filter by model id");
    Console.WriteLine("  --help, -h            Show help");
  }

  private static bool TryGetValue(string arg, string key, out string value)
  {
    value = string.Empty;
    if (!arg.StartsWith(key + "=", StringComparison.Ordinal))
    {
      return false;
    }

    value = arg[(key.Length + 1)..];
    return true;
  }

  private static bool TryGetNext(string[] args, ref int index, string key, out string value)
  {
    value = string.Empty;
    if (args[index] != key)
    {
      return false;
    }

    if (index + 1 >= args.Length)
    {
      throw new ArgumentException($"Missing value for {key}");
    }

    value = args[++index];
    return true;
  }
}
