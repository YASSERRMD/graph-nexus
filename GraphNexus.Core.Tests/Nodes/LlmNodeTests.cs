using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class MockLlmClient : ILlmClient
{
    public string ResponseToReturn { get; set; } = "Mock response";
    public int InvokeCount { get; private set; }
    public string? LastPrompt { get; private set; }

    public Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken cancellationToken = default)
    {
        InvokeCount++;
        LastPrompt = prompt;
        return Task.FromResult(ResponseToReturn);
    }
}

public class LlmNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallLlmClient()
    {
        var mockClient = new MockLlmClient { ResponseToReturn = "Hello!" };
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

        Assert.Contains("Hello World!", mockClient.LastPrompt);
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
        public Task<string> GenerateAsync(string prompt, string? model = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("LLM API failed");
        }
    }
}
