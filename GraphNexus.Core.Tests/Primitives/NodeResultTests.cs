using System.Text.Json;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Primitives;

public class NodeResultTests
{
    private static readonly JsonSerializerOptions Options = NodeResultSerializerOptions.Default;

    [Fact]
    public void SuccessResult_ShouldSerializeAndDeserialize()
    {
        var state = WorkflowState.Create("workflow-1");
        var result = new SuccessResult("node-1", "exec-1", state);

        var json = JsonSerializer.Serialize(result, Options);
        var deserialized = JsonSerializer.Deserialize<NodeResult>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<SuccessResult>(deserialized);
        Assert.Equal("node-1", deserialized.NodeId);
        Assert.Equal("exec-1", deserialized.ExecutionId);
    }

    [Fact]
    public void FailureResult_ShouldSerializeAndDeserialize()
    {
        var result = new FailureResult("node-1", "exec-1", "Validation failed", "Error details");

        var json = JsonSerializer.Serialize(result, Options);
        var deserialized = JsonSerializer.Deserialize<NodeResult>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<FailureResult>(deserialized);
        var failure = (FailureResult)deserialized;
        Assert.Equal("Validation failed", failure.Reason);
    }

    [Fact]
    public void SkippedResult_ShouldSerializeAndDeserialize()
    {
        var result = new SkippedResult("node-1", "exec-1", "Condition not met");

        var json = JsonSerializer.Serialize(result, Options);
        var deserialized = JsonSerializer.Deserialize<NodeResult>(json, Options);

        Assert.NotNull(deserialized);
        Assert.IsType<SkippedResult>(deserialized);
        var skipped = (SkippedResult)deserialized;
        Assert.Equal("Condition not met", skipped.Reason);
    }

    [Fact]
    public void NodeResult_ComputeHash_ShouldReturnConsistentHash()
    {
        var state = WorkflowState.Create("workflow-1");
        var result = new SuccessResult("node-1", "exec-1", state);

        var hash1 = result.ComputeHash();
        var hash2 = result.ComputeHash();

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
    }
}
