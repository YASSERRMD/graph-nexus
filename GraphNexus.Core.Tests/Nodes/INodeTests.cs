using GraphNexus.Nodes;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Nodes;

public class MockNode : INode
{
    private readonly Func<WorkflowState, CancellationToken, Task<NodeResult>> _executeFunc;
    private readonly IReadOnlyList<string> _inputKeys;
    private readonly IReadOnlyList<string> _outputKeys;

    public string Id { get; }
    public string Name { get; }

    public MockNode(
        string id,
        string name,
        Func<WorkflowState, CancellationToken, Task<NodeResult>>? executeFunc = null,
        IReadOnlyList<string>? inputKeys = null,
        IReadOnlyList<string>? outputKeys = null)
    {
        Id = id;
        Name = name;
        _executeFunc = executeFunc ?? ((state, ct) => Task.FromResult<NodeResult>(new SuccessResult(Id, "exec-" + Guid.NewGuid(), state)));
        _inputKeys = inputKeys ?? [];
        _outputKeys = outputKeys ?? [];
    }

    public Task<NodeResult> ExecuteAsync(WorkflowState state, CancellationToken cancellationToken = default)
    {
        return _executeFunc(state, cancellationToken);
    }

    public IReadOnlyList<string> GetInputKeys() => _inputKeys;
    public IReadOnlyList<string> GetOutputKeys() => _outputKeys;
}

public class INodeTests
{
    [Fact]
    public void INode_ShouldHaveUniqueIdAndName()
    {
        var node = new MockNode("node-1", "TestNode");

        Assert.Equal("node-1", node.Id);
        Assert.Equal("TestNode", node.Name);
    }

    [Fact]
    public async Task INode_ExecuteAsync_ShouldReturnSuccessResult()
    {
        var state = WorkflowState.Create("workflow-1");
        var node = new MockNode("node-1", "TestNode");

        var result = await node.ExecuteAsync(state);

        Assert.IsType<SuccessResult>(result);
        Assert.Equal("node-1", result.NodeId);
    }

    [Fact]
    public async Task INode_ExecuteAsync_CustomFunction_ShouldBeCalled()
    {
        var state = WorkflowState.Create("workflow-1");
        var executed = false;
        var node = new MockNode(
            "node-1",
            "TestNode",
            executeFunc: (s, ct) =>
            {
                executed = true;
                return Task.FromResult<NodeResult>(new FailureResult("node-1", "exec-1", "Custom failure"));
            }
        );

        var result = await node.ExecuteAsync(state);

        Assert.True(executed);
        Assert.IsType<FailureResult>(result);
    }

    [Fact]
    public void INode_GetInputKeys_ShouldReturnKeys()
    {
        var inputKeys = new List<string> { "input1", "input2" };
        var node = new MockNode("node-1", "TestNode", inputKeys: inputKeys);

        var keys = node.GetInputKeys();

        Assert.Equal(2, keys.Count);
        Assert.Contains("input1", keys);
    }

    [Fact]
    public void INode_GetOutputKeys_ShouldReturnKeys()
    {
        var outputKeys = new List<string> { "output1" };
        var node = new MockNode("node-1", "TestNode", outputKeys: outputKeys);

        var keys = node.GetOutputKeys();

        Assert.Single(keys);
        Assert.Contains("output1", keys);
    }
}
