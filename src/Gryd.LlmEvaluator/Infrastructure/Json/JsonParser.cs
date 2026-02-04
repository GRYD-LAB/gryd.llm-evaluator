using System.Text.Json;
using Gryd.LlmEvaluator.Application;

namespace Gryd.LlmEvaluator.Infrastructure.Json;

public sealed class JsonParser : IJsonParser
{
  public bool TryParse(string json, out JsonDocument? document, out string? error)
  {
    try
    {
      document = JsonDocument.Parse(json);
      error = null;
      return true;
    }
    catch (Exception ex)
    {
      document = null;
      error = ex.Message;
      return false;
    }
  }
}
