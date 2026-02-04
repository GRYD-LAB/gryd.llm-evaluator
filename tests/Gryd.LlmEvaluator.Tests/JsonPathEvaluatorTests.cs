using System.Text.Json;
using Gryd.LlmEvaluator.Domain;
using Xunit;

namespace Gryd.LlmEvaluator.Tests;

public sealed class JsonPathEvaluatorTests
{
    [Fact]
    public void TryEvaluate_ResolvesNestedPropertiesAndArrays()
    {
        var json = "{\"user\":{\"name\":\"Ana\"},\"items\":[{\"price\":12.5},{\"price\":30}]}";
        using var doc = JsonDocument.Parse(json);

        Assert.True(JsonPathEvaluator.TryEvaluate(doc.RootElement, "$.user.name", out var name));
        Assert.Equal("Ana", name.GetString());

        Assert.True(JsonPathEvaluator.TryEvaluate(doc.RootElement, "$.items[1].price", out var price));
        Assert.Equal(30, price.GetInt32());
    }
}
