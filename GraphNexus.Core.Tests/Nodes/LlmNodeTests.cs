using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class MockLlmClient : ILlmClient
{
    public LlmResponse? ResponseToReturn { get; set; } = new LlmResponse { Content = "Mock response" };
    public int InvokeCount { get; private set; }
    public LlmRequest? LastRequest { get; private set; }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        InvokeCount++;
        LastRequest = request;
        await Task.CompletedTask;
        return ResponseToReturn!;
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield return "Mock";
    }
}

public class LlmNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallLlmClient()
    {
        var mockClient = new MockLlmClient { ResponseToReturn = new LlmResponse { Content = "Hello!" } };
        var node = new LlmNode("llm-1", "ChatNode", mockClient, "Say hello");
        var state = WorkflowState.Create("workflow-1");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        Assert.Equal(1, mockClient.InvokeCount);
        Assert.Equal("Hello!", ((SuccessResult)result).OutputState.Data["llm_output"]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRenderTemplateWithStateData()
    {
        var mockClient = new MockLlmClient();
        var node = new LlmNode("llm-1", "ChatNode", mockClient, "Hello {{name}}!");
        var state = WorkflowState.Create("workflow-1").WithData("name", "World");

        var result = await node.ExecuteAsync(state);

        Assert.NotNull(mockClient.LastRequest);
        Assert.Contains("Hello World!", mockClient.LastRequest.Messages[0].Content);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomOutputKey_ShouldUseKey()
    {
        var mockClient = new MockLlmClient();
        var node = new LlmNode("llm-1", "ChatNode", mockClient, "Prompt", outputKey: "response");
        var state = WorkflowState.Create("workflow-1");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal("Mock response", success.OutputState.Data["response"]);
        Assert.False(success.OutputState.Data.ContainsKey("llm_output"));
    }

    [Fact]
    public async Task ExecuteAsync_WithModelAndTemperature_ShouldPassToClient()
    {
        var mockClient = new MockLlmClient();
        var node = new LlmNode("llm-1", "ChatNode", mockClient, "Prompt", model: "gpt-4", temperature: 0.7, maxTokens: 100);
        var state = WorkflowState.Create("workflow-1");

        var result = await node.ExecuteAsync(state);

        Assert.NotNull(mockClient.LastRequest);
        Assert.Equal("gpt-4", mockClient.LastRequest.Model);
        Assert.Equal(0.7, mockClient.LastRequest.Temperature);
        Assert.Equal(100, mockClient.LastRequest.MaxTokens);
    }

    [Fact]
    public void GetInputKeys_ShouldExtractTemplateVariables()
    {
        var mockClient = new MockLlmClient();
        var node = new LlmNode("llm-1", "ChatNode", mockClient, "Summarize {{text}} with {{style}} style");

        var keys = node.GetInputKeys();

        Assert.Contains("text", keys);
        Assert.Contains("style", keys);
    }

    [Fact]
    public async Task ExecuteAsync_WhenClientThrows_ShouldReturnFailure()
    {
        var failingClient = new FailingLlmClient();
        var node = new LlmNode("llm-1", "ChatNode", failingClient, "Prompt");
        var state = WorkflowState.Create("workflow-1");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<FailureResult>(result);
    }

    private class FailingLlmClient : ILlmClient
    {
        public Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("LLM API failed");
        }

        public async IAsyncEnumerable<string> GenerateStreamingAsync(LlmRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new InvalidOperationException("LLM API failed");
        }
    }
}
