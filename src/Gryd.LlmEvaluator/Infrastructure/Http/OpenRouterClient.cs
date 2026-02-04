using System.Net.Http.Json;
using System.Text.Json;
using Gryd.LlmEvaluator.Application;
using Gryd.LlmEvaluator.Domain;

namespace Gryd.LlmEvaluator.Infrastructure.Http;

public sealed class OpenRouterClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly int _maxAttempts;
    private readonly int _backoffMs;

    public OpenRouterClient(HttpClient httpClient, RetryConfig retry)
    {
        _httpClient = httpClient;
        _maxAttempts = Math.Max(1, retry.MaxAttempts);
        _backoffMs = Math.Max(0, retry.BackoffMs);
    }

    public async Task<string> ExecuteAsync(string modelId, string prompt, LlmParams llm, CancellationToken ct)
    {
        var payload = new
        {
            model = modelId,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = llm.Temperature,
            top_p = llm.TopP,
            max_tokens = llm.MaxTokens
        };

        string? lastError = null;
        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
                {
                    Content = JsonContent.Create(payload)
                };

                using var response = await _httpClient.SendAsync(request, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    lastError = $"OpenRouter {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
                    await BackoffAsync(attempt, ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    lastError = $"OpenRouter {(int)response.StatusCode} {response.ReasonPhrase}: {body}";
                    break;
                }

                return ExtractContent(body);
            }
            catch (HttpRequestException ex) when (attempt < _maxAttempts)
            {
                lastError = ex.Message;
                await BackoffAsync(attempt, ct);
            }
        }

        throw new InvalidOperationException(lastError ?? "Failed to get response from OpenRouter after retries.");
    }

    private async Task BackoffAsync(int attempt, CancellationToken ct)
    {
        if (attempt >= _maxAttempts)
        {
            return;
        }

        var delay = _backoffMs * attempt;
        if (delay > 0)
        {
            await Task.Delay(delay, ct);
        }
    }

    private static string ExtractContent(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Invalid OpenRouter response: choices missing.");
        }

        var first = choices.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("Invalid OpenRouter response: choices empty.");
        }

        if (!first.TryGetProperty("message", out var message) || !message.TryGetProperty("content", out var content))
        {
            throw new InvalidOperationException("Invalid OpenRouter response: message content missing.");
        }

        return content.GetString() ?? string.Empty;
    }
}
