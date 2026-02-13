using GraphNexus.Agents;
using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Agents;

public class TestAgent : AgentBase
{
    public TestAgent(
        string id,
        string name,
        ILlmClient llmClient,
        IReadOnlyList<INode>? tools = null,
        IMemorySelector? memorySelector = null,
        string? systemPrompt = null)
        : base(id, name, llmClient, tools, memorySelector, systemPrompt)
    {
    }

    protected override string BuildAgentPrompt(WorkflowState state)
    {
        return state.Data.TryGetValue("task", out var task) ? task?.ToString() ?? "" : "Complete the task.";
    }
}

public class AgentBaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldCallLlmClient()
    {
        var mockClient = new MockLlmClient
        {
            ResponseToReturn = new LlmResponse { Content = "Agent response" }
        };
        var agent = new TestAgent("agent-1", "TestAgent", mockClient);
        var state = WorkflowState.Create("workflow-1");

        var result = await agent.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        Assert.Equal(1, mockClient.InvokeCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStoreResponseInState()
    {
        var mockClient = new MockLlmClient
        {
            ResponseToReturn = new LlmResponse { Content = "Response" }
        };
        var agent = new TestAgent("agent-1", "TestAgent", mockClient);
        var state = WorkflowState.Create("workflow-1");

        var result = await agent.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal("Response", success.OutputState.Data["agent_response"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithMemorySelector_ShouldUseSelector()
    {
        var mockClient = new MockLlmClient
        {
            ResponseToReturn = new LlmResponse { Content = "Response" }
        };
        var selector = new LastNMessagesSelector(2);
        var agent = new TestAgent("agent-1", "TestAgent", mockClient, memorySelector: selector);
        var state = WorkflowState.Create("workflow-1")
            .WithMessage(Message.Create("user", "Message 1"))
            .WithMessage(Message.Create("user", "Message 2"))
            .WithMessage(Message.Create("user", "Message 3"));

        await agent.ExecuteAsync(state);

        Assert.NotNull(mockClient.LastRequest);
    }

    [Fact]
    public async Task ExecuteAsync_WithTools_ShouldIncludeToolsInRequest()
    {
        var mockClient = new MockLlmClient
        {
            ResponseToReturn = new LlmResponse { Content = "Response" }
        };
        var mockTool = new Core.Nodes.PassthroughNode("tool-1", "Passthrough");
        var agent = new TestAgent("agent-1", "TestAgent", mockClient, tools: [mockTool]);
        var state = WorkflowState.Create("workflow-1");

        await agent.ExecuteAsync(state);

        Assert.NotNull(mockClient.LastRequest?.Tools);
        Assert.Contains(mockClient.LastRequest.Tools.Keys, k => k == "Passthrough");
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomSystemPrompt_ShouldUseIt()
    {
        var mockClient = new MockLlmClient
        {
            ResponseToReturn = new LlmResponse { Content = "Response" }
        };
        var agent = new TestAgent("agent-1", "TestAgent", mockClient, systemPrompt: "Custom system prompt");
        var state = WorkflowState.Create("workflow-1");

        await agent.ExecuteAsync(state);

        Assert.NotNull(mockClient.LastRequest?.SystemPrompt);
    }

    [Fact]
    public void GetOutputKeys_ShouldReturnAgentResponse()
    {
        var mockClient = new MockLlmClient();
        var agent = new TestAgent("agent-1", "TestAgent", mockClient);

        var keys = agent.GetOutputKeys();

        Assert.Contains("agent_response", keys);
    }
}
