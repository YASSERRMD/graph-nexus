using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GraphNexus.Primitives;

namespace GraphNexus.Core.Nodes;

public sealed class OpenAiClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAiClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "https://api.openai.com/v1";
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = request.Model ?? "gpt-4",
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            tools = request.Tools
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadFromJsonAsync<OpenAiResponse>(_jsonOptions, cancellationToken);

        if (responseJson?.Choices == null || responseJson.Choices.Count == 0)
        {
            throw new InvalidOperationException("No response from OpenAI");
        }

        var choice = responseJson.Choices[0];
        return new LlmResponse
        {
            Content = choice.Message?.Content ?? "",
            Model = responseJson.Model,
            TokensUsed = responseJson.Usage?.TotalTokens ?? 0,
            FinishReason = choice.FinishReason
        };
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            model = request.Model ?? "gpt-4",
            messages = request.Messages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: "))
                continue;

            var data = line[6..];
            if (data == "[DONE]")
                break;

            var chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(data, _jsonOptions);
            if (chunk?.Choices?.Count > 0)
            {
                var delta = chunk.Choices[0].Delta?.Content;
                if (!string.IsNullOrEmpty(delta))
                {
                    yield return delta;
                }
            }
        }
    }

    private class OpenAiResponse
    {
        public string? Model { get; set; }
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
        public string? FinishReason { get; set; }
    }

    private class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private class OpenAiStreamChunk
    {
        public List<OpenAiStreamChoice>? Choices { get; set; }
    }

    private class OpenAiStreamChoice
    {
        public OpenAiStreamDelta? Delta { get; set; }
    }

    private class OpenAiStreamDelta
    {
        public string? Content { get; set; }
    }

    private class OpenAiUsage
    {
        public int TotalTokens { get; set; }
    }
}
