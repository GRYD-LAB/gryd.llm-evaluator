using Gryd.LlmEvaluator.Infrastructure.Config;
using Xunit;

namespace Gryd.LlmEvaluator.Tests;

public sealed class YamlConfigLoaderTests
{
  [Fact]
  public async Task LoadAsync_ParsesConfigurationFiles()
  {
    var tempDir = Path.Combine(Path.GetTempPath(), "gryd-" + Guid.NewGuid());
    Directory.CreateDirectory(tempDir);

    var configPath = Path.Combine(tempDir, "config.yml");
    var modelsPath = Path.Combine(tempDir, "models.yml");
    var templatesDir = Path.Combine(tempDir, "templates");
    Directory.CreateDirectory(templatesDir);
    var templatesPath = Path.Combine(templatesDir, "intent.yml");

    await File.WriteAllTextAsync(modelsPath, """
models:
  - id: \"test-model\"
""");

    await File.WriteAllTextAsync(templatesPath, """
templates:
  - id: "sample"
    description: "Sample template"
    prompt_template: "Hello {{name}}"
    vars:
      system_instructions: "Be strict"
      extra_rules: |
        1. First rule
        2. Second rule
      agent_domain: "general"
    llm:
      temperature: 0.2
      top_p: 1.0
      max_tokens: 50
    assertions:
      - path: "$.name"
        type: "string"
        enum: ["Ana", "Joao"]
        min_len: 2
        required: true
        weight: 2
    scenarios:
      - description: "Simple name scenario"
        vars:
          name: "Ana"
""");

    await File.WriteAllTextAsync(configPath, $"""
models_file: {modelsPath}
templates_dir: {templatesDir}
runs: 3
concurrency: 2
retry:
  max_attempts: 2
  backoff_ms: 100
output:
  default_path: ./reports/report.json
""");

    var loader = new YamlConfigLoader();
    var (app, models, templates) = await loader.LoadAsync(configPath, CancellationToken.None);

    Assert.Equal(3, app.Runs);
    Assert.Equal(2, app.Concurrency);
    Assert.Single(models.Models);
    Assert.Single(templates.Templates);
    Assert.Single(templates.Templates[0].Scenarios);
    Assert.Equal(2, templates.Templates[0].Assertions[0].Enum?.Count);
    Assert.Equal("general", templates.Templates[0].Vars["agent_domain"]);
    Assert.Contains("First rule", templates.Templates[0].Vars["extra_rules"]);
  }
}
