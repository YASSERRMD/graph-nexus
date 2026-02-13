using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Moq;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class OpenAiClientTests
{
    private readonly MockHttpMessageHandler _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpenAiClientTests()
    {
        _mockHandler = new MockHttpMessageHandler();
        _httpClient = new HttpClient(_mockHandler);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    [Fact]
    public async Task GenerateAsync_WithValidResponse_ShouldReturnContent()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "gpt-4",
            choices = new[]
            {
                new { message = new { content = "Hello, world!" }, finish_reason = "stop" }
            },
            usage = new { total_tokens = 10 }
        }, _jsonOptions);

        _mockHandler.SetupResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenAiClient("test-key", httpClient: _httpClient);
        var request = new LlmRequest
        {
            Messages = new List<Message> { Message.Create("user", "Say hello") },
            Model = "gpt-4"
        };

        var result = await client.GenerateAsync(request);

        Assert.Equal("Hello, world!", result.Content);
        Assert.Equal("gpt-4", result.Model);
    }

    [Fact]
    public async Task GenerateAsync_WithAzureEndpoint_ShouldUseCustomBaseUrl()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "gpt-35-turbo",
            choices = new[]
            {
                new { message = new { content = "Response" }, finish_reason = "stop" }
            }
        }, _jsonOptions);

        _mockHandler.SetupResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenAiClient("test-key", "https://my-resource.openai.azure.com/openai", _httpClient);
        var request = new LlmRequest
        {
            Messages = new List<Message> { Message.Create("user", "Test") }
        };

        await client.GenerateAsync(request);

        Assert.Contains("my-resource.openai.azure.com", _mockHandler.LastRequest?.RequestUri?.ToString() ?? "");
    }

    [Fact]
    public async Task GenerateAsync_WithInvalidResponse_ShouldThrow()
    {
        _mockHandler.SetupResponse(HttpStatusCode.BadRequest, "Invalid request");

        var client = new OpenAiClient("test-key", httpClient: _httpClient);
        var request = new LlmRequest
        {
            Messages = new List<Message> { Message.Create("user", "Test") }
        };

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GenerateAsync(request));
    }

    [Fact]
    public async Task GenerateAsync_WithNullChoices_ShouldThrow()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            model = "gpt-4",
            choices = Array.Empty<object>()
        }, _jsonOptions);

        _mockHandler.SetupResponse(HttpStatusCode.OK, responseJson);

        var client = new OpenAiClient("test-key", httpClient: _httpClient);
        var request = new LlmRequest
        {
            Messages = new List<Message> { Message.Create("user", "Test") }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GenerateAsync(request));
    }

    [Fact]
    public async Task GenerateAsync_ShouldIncludeAuthHeader()
    {
        _mockHandler.SetupResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            model = "gpt-4",
            choices = new[] { new { message = new { content = "ok" }, finish_reason = "stop" } }
        }, _jsonOptions));

        var client = new OpenAiClient("test-api-key", httpClient: _httpClient);
        var request = new LlmRequest
        {
            Messages = new List<Message> { Message.Create("user", "Test") }
        };

        await client.GenerateAsync(request);

        Assert.NotNull(_mockHandler.LastRequest);
        Assert.True(_mockHandler.LastRequest.Headers.Authorization?.ToString().Contains("test-api-key") ?? false);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage? _response;
        private string _responseBody = "";
        private HttpStatusCode _statusCode = HttpStatusCode.OK;

        public Uri? LastRequest { get; private set; }

        public void SetupResponse(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _responseBody = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;

            await Task.CompletedTask;

            var response = new HttpResponseMessage(_statusCode);
            response.Content = new StringContent(_responseBody, Encoding.UTF8, "application/json");
            return response;
        }
    }
}
