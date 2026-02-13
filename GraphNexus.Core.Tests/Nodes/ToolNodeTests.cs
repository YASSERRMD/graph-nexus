using GraphNexus.Core.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class StringLengthTool : ITool<string, int>
{
    public string Name => "StringLength";
    public string Description => "Returns the length of a string";

    public Task<int> InvokeAsync(string input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(input.Length);
    }
}

public class AddNumbersTool : ITool<AddNumbersInput, int>
{
    public string Name => "AddNumbers";
    public string Description => "Adds two numbers together";

    public Task<int> InvokeAsync(AddNumbersInput input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(input.A + input.B);
    }
}

public record AddNumbersInput(int A, int B);

public class ToolNodeTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldInvokeToolAndMapOutput()
    {
        var tool = new StringLengthTool();
        var node = new ToolNode<string, int>("tool-1", "StringLength", tool);
        var state = WorkflowState.Create("workflow-1").WithData("input", "hello");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal(5, success.OutputState.Data["output"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomInputMapper_ShouldUseMapper()
    {
        var tool = new StringLengthTool();
        var node = new ToolNode<string, int>(
            "tool-1",
            "StringLength",
            tool,
            inputMapper: state => state.Data["text"]?.ToString() ?? ""
        );
        var state = WorkflowState.Create("workflow-1").WithData("text", "world");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal(5, success.OutputState.Data["output"]);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomOutputMapper_ShouldUseMapper()
    {
        var tool = new AddNumbersTool();
        var node = new ToolNode<AddNumbersInput, int>(
            "tool-1",
            "AddNumbers",
            tool,
            outputMapper: (state, result) => state.WithData("sum", result)
        );
        var state = WorkflowState.Create("workflow-1").WithData("a", 3).WithData("b", 7);

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        var success = (SuccessResult)result;
        Assert.Equal(10, success.OutputState.Data["sum"]);
        Assert.False(success.OutputState.Data.ContainsKey("output"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolThrows_ShouldReturnFailure()
    {
        var failingTool = new FailingTool();
        var node = new ToolNode<string, string>("tool-1", "FailingTool", failingTool);
        var state = WorkflowState.Create("workflow-1").WithData("input", "test");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<FailureResult>(result);
        var failure = (FailureResult)result;
        Assert.Equal("Tool failed", failure.Reason);
    }

    private class FailingTool : ITool<string, string>
    {
        public string Name => "FailingTool";
        public string Description => "Always fails";

        public Task<string> InvokeAsync(string input, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Tool failed");
        }
    }
}
