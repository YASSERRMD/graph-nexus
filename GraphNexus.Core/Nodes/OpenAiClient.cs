using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
    private readonly HashSet<string> _allowedHosts;

    public OpenAiClient(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        ArgumentNullException.ThrowIfNull(apiKey);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be empty", nameof(apiKey));

        if (apiKey.StartsWith("sk-") == false && apiKey.Length < 20)
            throw new ArgumentException("Invalid API key format", nameof(apiKey));

        _apiKey = apiKey;
        _baseUrl = SanitizeUrl(baseUrl ?? "https://api.openai.com/v1");

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        };

        _httpClient = httpClient ?? new HttpClient(handler)
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _allowedHosts = new HashSet<string>
        {
            new Uri(_baseUrl).Host,
            "api.openai.com",
            "api.azure.com",
            "openai.azure.com"
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var sanitizedMessages = request.Messages.Select(SanitizeMessage).ToList();

        var payload = new
        {
            model = request.Model ?? "gpt-4",
            messages = sanitizedMessages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            tools = request.Tools
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"OpenAI API error: {response.StatusCode} - {errorContent}");
        }

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
        ValidateRequest(request);

        var sanitizedMessages = request.Messages.Select(SanitizeMessage).ToList();

        var payload = new
        {
            model = request.Model ?? "gpt-4",
            messages = sanitizedMessages.Select(m => new { role = m.Role, content = m.Content }),
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("/chat/completions", content, cancellationToken);
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

    private void ValidateRequest(LlmRequest request)
    {
        if (request.Messages == null || request.Messages.Count == 0)
        {
            throw new ArgumentException("Request must contain at least one message");
        }

        foreach (var message in request.Messages)
        {
            if (message == null)
                throw new ArgumentException("Message cannot be null");

            if (string.IsNullOrWhiteSpace(message.Content) && (message.ToolCalls == null || message.ToolCalls.Count == 0))
            {
                throw new ArgumentException("Message content cannot be empty");
            }
        }

        if (!string.IsNullOrEmpty(request.Model))
        {
            ValidateModelName(request.Model);
        }
    }

    private void ValidateModelName(string model)
    {
        var invalidPatterns = new[] { "../", "/etc/", "file://", "http://", "https://" };
        var lowerModel = model.ToLowerInvariant();

        foreach (var pattern in invalidPatterns)
        {
            if (lowerModel.Contains(pattern))
            {
                throw new ArgumentException($"Invalid model name: {model}");
            }
        }
    }

    private Message SanitizeMessage(Message message)
    {
        var sanitizedContent = SanitizeInput(message.Content);
        return message with { Content = sanitizedContent };
    }

    private string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sanitized = input
            .Replace("\u0000", "")
            .Replace("\u200B", "")
            .Replace("\u200C", "")
            .Replace("\u200D", "")
            .Trim();

        if (sanitized.Length > 100000)
        {
            sanitized = sanitized[..100000];
        }

        return sanitized;
    }

    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Base URL cannot be empty", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid base URL format", nameof(url));

        if (uri.Scheme != "https")
            throw new ArgumentException("Only HTTPS URLs are allowed", nameof(url));

        var suspiciousPatterns = new[] { "../", "/..", "%2e%2e", "localhost", "127.0.0.1", "0.0.0.0" };
        var lowerUrl = url.ToLowerInvariant();

        foreach (var pattern in suspiciousPatterns)
        {
            if (lowerUrl.Contains(pattern))
                throw new ArgumentException($"URL contains suspicious pattern: {pattern}", nameof(url));
        }

        return url.TrimEnd('/');
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
