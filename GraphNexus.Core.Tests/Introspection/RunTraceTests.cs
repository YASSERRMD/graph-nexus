using GraphNexus.Introspection;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Introspection;

public class RunTraceTests
{
    private WorkflowState CreateState(string workflowId = "workflow-1") => 
        WorkflowState.Create(workflowId, "thread-1");

    [Fact]
    public void RunTrace_Duration_WhenCompleted_ShouldCalculateCorrectly()
    {
        var startedAt = DateTimeOffset.Parse("2024-01-01T10:00:00Z");
        var completedAt = DateTimeOffset.Parse("2024-01-01T10:05:00Z");

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            WorkflowId = "workflow-1",
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

        Assert.Equal(TimeSpan.FromMinutes(5), trace.Duration);
    }

    [Fact]
    public void RunTrace_IsCompleted_WhenCompletedEventExists_ShouldReturnTrue()
    {
        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent>
            {
                new WorkflowCompletedEvent("evt-1", "exec-1", "end", CreateState())
            }
        };

        Assert.True(trace.IsCompleted);
    }

    [Fact]
    public void GetNodeExecutions_ShouldReturnCorrectExecutions()
    {
        var state = CreateState();
        var entered = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);
        var exited = new NodeExitedEvent("evt-2", "exec-1", "node-1", state);

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent> { entered, exited }
        };

        var executions = trace.GetNodeExecutions();

        Assert.Single(executions);
        Assert.Equal("node-1", executions[0].NodeId);
    }

    [Fact]
    public void GetErrors_ShouldReturnAllErrors()
    {
        var state = CreateState();
        var error1 = new NodeErrorEvent("evt-1", "exec-1", "node-1", state, "Error 1", "stack");
        var error2 = new NodeErrorEvent("evt-2", "exec-1", "node-2", state, "Error 2", "stack");

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent> { error1, error2 }
        };

        var errors = trace.GetErrors();

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void GetEventsByNode_ShouldFilterCorrectly()
    {
        var state = CreateState();
        var entered = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);
        var exited = new NodeExitedEvent("evt-2", "exec-1", "node-1", state);
        var otherEntered = new NodeEnteredEvent("evt-3", "exec-1", "node-2", state);

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent> { entered, exited, otherEntered }
        };

        var node1Events = trace.GetEventsByNode("node-1");

        Assert.Equal(2, node1Events.Count);
    }

    [Fact]
    public void GetEventsByType_ShouldFilterCorrectly()
    {
        var state = CreateState();
        var entered = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);
        var exited = new NodeExitedEvent("evt-2", "exec-1", "node-1", state);
        var error = new NodeErrorEvent("evt-3", "exec-1", "node-1", state, "error", "stack");

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent> { entered, exited, error }
        };

        var enteredEvents = trace.GetEventsByType(StateEventType.NodeEntered);
        var errorEvents = trace.GetEventsByType(StateEventType.NodeError);

        Assert.Single(enteredEvents);
        Assert.Single(errorEvents);
    }

    [Fact]
    public void RunTraceAnalyzer_GetStatistics_ShouldCalculateCorrectly()
    {
        var state = CreateState();
        var now = DateTimeOffset.UtcNow;

        var entered1 = new NodeEnteredEvent("evt-1", "exec-1", "node-1", state);
        var exited1 = new NodeExitedEvent("evt-2", "exec-1", "node-1", state);

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            StartedAt = now,
            CompletedAt = now.AddMinutes(10),
            Events = new List<StateEvent> { entered1, exited1 }
        };

        var analyzer = new RunTraceAnalyzer(trace);
        var stats = analyzer.GetStatistics();

        Assert.Equal(1, stats.TotalNodesExecuted);
        Assert.Equal(0, stats.TotalErrors);
        Assert.Equal(TimeSpan.FromMinutes(10), stats.TotalDuration);
    }

    [Fact]
    public void RunTraceAnalyzer_HasErrors_WhenErrorsExist_ShouldReturnTrue()
    {
        var state = CreateState();
        var error = new NodeErrorEvent("evt-1", "exec-1", "node-1", state, "Error", "stack");

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent> { error }
        };

        var analyzer = new RunTraceAnalyzer(trace);

        Assert.True(analyzer.HasErrors());
    }

    [Fact]
    public void RunTraceAnalyzer_IsHealthy_WhenNoErrorsAndCompleted_ShouldReturnTrue()
    {
        var state = CreateState();

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent>
            {
                new WorkflowCompletedEvent("evt-1", "exec-1", "end", state)
            }
        };

        var analyzer = new RunTraceAnalyzer(trace);

        Assert.True(analyzer.IsHealthy());
    }

    [Fact]
    public void GetExecutionPath_ShouldReturnOrderedNodeIds()
    {
        var state = CreateState();

        var trace = new RunTrace
        {
            ExecutionId = "exec-1",
            Events = new List<StateEvent>
            {
                new NodeEnteredEvent("evt-1", "exec-1", "node-a", state),
                new NodeEnteredEvent("evt-2", "exec-1", "node-b", state),
                new NodeEnteredEvent("evt-3", "exec-1", "node-c", state)
            }
        };

        var analyzer = new RunTraceAnalyzer(trace);
        var path = analyzer.GetExecutionPath();

        Assert.Equal(3, path.Count);
        Assert.Equal("node-a", path[0]);
        Assert.Equal("node-b", path[1]);
        Assert.Equal("node-c", path[2]);
    }
}
