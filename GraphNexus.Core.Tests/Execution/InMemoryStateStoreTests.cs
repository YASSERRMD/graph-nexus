using GraphNexus.Execution;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Execution;

public class InMemoryStateStoreTests
{
    [Fact]
    public async Task GetStateAsync_WhenNotFound_ShouldReturnNull()
    {
        var store = new InMemoryStateStore();

        var result = await store.GetStateAsync("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveStateAsync_ShouldStoreState()
    {
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state);
        var result = await store.GetStateAsync(state.Id);

        Assert.NotNull(result);
        Assert.Equal(state.Id, result.Id);
    }

    [Fact]
    public async Task SaveStateAsync_ShouldUpdateExistingState()
    {
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1");
        await store.SaveStateAsync(state);

        var updated = state.WithStep(10);
        await store.SaveStateAsync(updated);
        var result = await store.GetStateAsync(state.Id);

        Assert.NotNull(result);
        Assert.Equal(10, result.Step);
    }

    [Fact]
    public async Task GetStatesByWorkflowIdAsync_ShouldReturnWorkflowStates()
    {
        var store = new InMemoryStateStore();
        var state1 = WorkflowState.Create("workflow-1");
        var state2 = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state1);
        await store.SaveStateAsync(state2);
        var results = await store.GetStatesByWorkflowIdAsync("workflow-1");

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetStatesByThreadIdAsync_ShouldReturnThreadStates()
    {
        var store = new InMemoryStateStore();
        var threadId = "thread-123";
        var state1 = WorkflowState.Create("workflow-1", threadId);
        var state2 = WorkflowState.Create("workflow-2", threadId);

        await store.SaveStateAsync(state1);
        await store.SaveStateAsync(state2);
        var results = await store.GetStatesByThreadIdAsync(threadId);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task DeleteStateAsync_ShouldRemoveState()
    {
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1");

        await store.SaveStateAsync(state);
        await store.DeleteStateAsync(state.Id);
        var result = await store.GetStateAsync(state.Id);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnCorrectStatus()
    {
        var store = new InMemoryStateStore();
        var state = WorkflowState.Create("workflow-1");

        var before = await store.ExistsAsync(state.Id);
        await store.SaveStateAsync(state);
        var after = await store.ExistsAsync(state.Id);

        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllStates()
    {
        var store = new InMemoryStateStore();
        await store.SaveStateAsync(WorkflowState.Create("workflow-1"));
        await store.SaveStateAsync(WorkflowState.Create("workflow-2"));

        await store.ClearAsync();
        var count = (await store.GetStatesByWorkflowIdAsync("workflow-1")).Count +
                    (await store.GetStatesByWorkflowIdAsync("workflow-2")).Count;

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task ConcurrentSaveAsync_ShouldHandleParallelWrites()
    {
        var store = new InMemoryStateStore();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => store.SaveStateAsync(WorkflowState.Create($"workflow-{i}")));

        await Task.WhenAll(tasks);

        Assert.Equal(100, (await store.GetStatesByWorkflowIdAsync("workflow-0")).Count);
    }
}
