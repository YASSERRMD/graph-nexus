using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class PassthroughNodeTests
{
    [Fact]
    public async Task ExecuteAsync_WithoutKeys_ShouldReturnSameState()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough");
        var state = WorkflowState.Create("workflow-1");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Empty(success.OutputState.Data);
    }

    [Fact]
    public async Task ExecuteAsync_WithInputKey_ShouldCopyValueToOutputKey()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough", "input", "output");
        var state = WorkflowState.Create("workflow-1").WithData("input", "test-value");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal("test-value", success.OutputState.Data["output"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameInputOutputKey_ShouldPreserveValue()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough", "key", "key");
        var state = WorkflowState.Create("workflow-1").WithData("key", 42);

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal(42, success.OutputState.Data["key"]);
    }

    [Fact]
    public async Task ExecuteAsync_MissingInputKey_ShouldReturnOriginalState()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough", "missing", "output");
        var state = WorkflowState.Create("workflow-1").WithData("other", "value");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.False(success.OutputState.Data.ContainsKey("output"));
    }

    [Fact]
    public void GetInputKeys_ShouldReturnConfiguredKey()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough", "input", "output");

        var keys = node.GetInputKeys();

        Assert.Single(keys);
        Assert.Equal("input", keys[0]);
    }

    [Fact]
    public void GetOutputKeys_ShouldReturnConfiguredKey()
    {
        var node = new PassthroughNode("passthrough-1", "Passthrough", "input", "output");

        var keys = node.GetOutputKeys();

        Assert.Single(keys);
        Assert.Equal("output", keys[0]);
    }
}
