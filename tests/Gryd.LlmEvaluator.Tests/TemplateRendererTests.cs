using Gryd.LlmEvaluator.Domain;
using Xunit;

namespace Gryd.LlmEvaluator.Tests;

public sealed class TemplateRendererTests
{
    [Fact]
    public void Render_ReplacesVariables_AndLeavesMissingEmpty()
    {
        var template = "Hello {{name}}, instructions: {{system_instructions}}, extra={{missing}}";
        var vars = new Dictionary<string, string>
        {
            ["name"] = "Guilherme",
            ["system_instructions"] = "Be strict"
        };

        var result = TemplateRenderer.Render(template, vars);

        Assert.Equal("Hello Guilherme, instructions: Be strict, extra=", result);
    }
}
