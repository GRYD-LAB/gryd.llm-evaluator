using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;
using Gryd.LlmEvaluator.Infrastructure.Json;
using Xunit;

namespace Gryd.LlmEvaluator.Tests;

public sealed class RunEvaluationUseCaseTests
{
  [Fact]
  public async Task ExecuteAsync_HandlesOverridesAndInvalidJson()
  {
    var models = new ModelsConfig(new List<ModelDefinition> { new("test-model") });
    var templates = new TemplateConfig(new List<TemplateDefinition>
        {
            new(
                "name_template",
                "Template description",
                "Hello {{name}}",
                new Dictionary<string, string>(),
                new LlmParams(0.2, 1.0, 100),
                new List<AssertionRule>
                {
                    new("$.name", "string", true, 3, 2, null, null, null, null, null, null)
                },
                new List<ScenarioDefinition>
                {
                    new(
                        "Short name triggers override",
                        new Dictionary<string, string> { ["name"] = "Ana" },
                        new List<AssertionRule>(),
                        new List<AssertionRule>
                        {
                            new("$.name", "string", true, 3, 5, null, null, null, null, null, null)
                        }
                    )
                }
            )
        });

    var client = new SequenceLLMClient(new[] { "{\"name\":\"Ana\"}", "not-json" });
    var jsonParser = new JsonParser();
    var useCase = new RunEvaluationUseCase(client, jsonParser, 0);

    var report = await useCase.ExecuteAsync(models, templates, 2, 1, null, null, CancellationToken.None);
    var scenario = report.Models[0].Templates[0].Scenarios[0];

    Assert.Equal(2, scenario.Runs.Count);
    Assert.Equal(0, scenario.Runs[0].Score); // override min_len=5 causes failure
    Assert.Equal(0, scenario.Runs[1].Score); // invalid JSON
  }

  private sealed class SequenceLLMClient : ILLMClient
  {
    private readonly Queue<string> _responses;

    public SequenceLLMClient(IEnumerable<string> responses)
    {
      _responses = new Queue<string>(responses);
    }

    public Task<string> ExecuteAsync(string modelId, string prompt, LlmParams llm, CancellationToken ct)
    {
      if (_responses.Count == 0)
      {
        throw new InvalidOperationException("No responses left.");
      }

      return Task.FromResult(_responses.Dequeue());
    }
  }
}
