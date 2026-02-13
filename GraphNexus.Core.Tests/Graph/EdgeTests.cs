using GraphNexus.Graph;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Graph;

public class EdgeTests
{
    [Fact]
    public void Edge_Create_ShouldInitializeCorrectly()
    {
        var edge = new Edge("node-a", "node-b", "next");

        Assert.Equal("node-a", edge.SourceNodeId);
        Assert.Equal("node-b", edge.TargetNodeId);
        Assert.Equal("next", edge.Label);
    }

    [Fact]
    public void Edge_EvaluateCondition_WithoutPredicate_ShouldReturnTrue()
    {
        var edge = new Edge("node-a", "node-b");
        var state = WorkflowState.Create("workflow-1");

        var result = edge.EvaluateCondition(state);

        Assert.True(result);
    }

    [Fact]
    public void Edge_EvaluateCondition_WithPredicate_ShouldReturnPredicateResult()
    {
        var edge = new Edge("node-a", "node-b", predicate: EdgePredicates.WhenDataEquals("key", "value"));
        var state = WorkflowState.Create("workflow-1").WithData("key", "value");

        var result = edge.EvaluateCondition(state);

        Assert.True(result);
    }

    [Fact]
    public void Edge_EvaluateCondition_WithPredicateNotMet_ShouldReturnFalse()
    {
        var edge = new Edge("node-a", "node-b", predicate: EdgePredicates.WhenDataEquals("key", "value"));
        var state = WorkflowState.Create("workflow-1").WithData("key", "other");

        var result = edge.EvaluateCondition(state);

        Assert.False(result);
    }

    [Fact]
    public void EdgePredicates_WhenStepGreaterThan_ShouldWork()
    {
        var predicate = EdgePredicates.WhenStepGreaterThan(5);
        var state = WorkflowState.Create("workflow-1").WithStep(10);

        var result = predicate(state);

        Assert.True(result);
    }

    [Fact]
    public void EdgePredicates_WhenStepLessThan_ShouldWork()
    {
        var predicate = EdgePredicates.WhenStepLessThan(3);
        var state = WorkflowState.Create("workflow-1").WithStep(1);

        var result = predicate(state);

        Assert.True(result);
    }

    [Fact]
    public void EdgePredicates_WhenDataContainsKey_ShouldWork()
    {
        var predicate = EdgePredicates.WhenDataContainsKey("result");
        var state = WorkflowState.Create("workflow-1").WithData("result", "data");

        var result = predicate(state);

        Assert.True(result);
    }

    [Fact]
    public void EdgePredicates_WhenStatusIs_ShouldWork()
    {
        var predicate = EdgePredicates.WhenStatusIs(WorkflowStatus.Running);
        var state = WorkflowState.Create("workflow-1");

        var result = predicate(state);

        Assert.True(result);
    }

    [Fact]
    public void EdgePredicates_Always_ShouldReturnTrue()
    {
        var predicate = EdgePredicates.Always;
        var state = WorkflowState.Create("workflow-1").WithStep(100);

        var result = predicate(state);

        Assert.True(result);
    }

    [Fact]
    public void EdgePredicates_Custom_ShouldUseDelegate()
    {
        var called = false;
        var predicate = EdgePredicates.Custom(s =>
        {
            called = true;
            return s.Step > 0;
        });
        var state = WorkflowState.Create("workflow-1").WithStep(1);

        var result = predicate(state);

        Assert.True(called);
        Assert.True(result);
    }
}
