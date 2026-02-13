using System.Threading.Channels;
using GraphNexus.Execution;
using GraphNexus.Graph;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public sealed class ParallelExecutor
{
    private readonly IStateStore _stateStore;
    private readonly SemaphoreSlim _concurrencySemaphore;

    public ParallelExecutor(IStateStore stateStore, int maxConcurrency = 4)
    {
        _stateStore = stateStore;
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async IAsyncEnumerable<StateEvent> RunAsync(
        ExecutionRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var context = new ExecutionContext(
            request.ExecutionId,
            request.WorkflowId,
            request.ThreadId,
            request.Graph,
            _stateStore,
            request.Options,
            cancellationToken
        );

        var currentState = request.InitialState;
        await _stateStore.SaveStateAsync(currentState, cancellationToken);

        var completedNodes = new ConcurrentBag<string>();
        var pendingNodes = new ConcurrentQueue<string>();
        var runningTasks = new List<Task>();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (!string.IsNullOrEmpty(request.Graph.EntryNodeId))
        {
            pendingNodes.Enqueue(request.Graph.EntryNodeId);
        }

        async Task ProcessNodeAsync(string nodeId, CancellationToken ct)
        {
            await _concurrencySemaphore.WaitAsync(ct);

            try
            {
                if (!request.Graph.Nodes.TryGetValue(nodeId, out var node))
                    return;

                var previousHash = StateEventHelpers.ComputeStateHash(currentState);

                var enteredEvent = StateEventHelpers.CreateEnteredEvent(
                    request.ExecutionId,
                    nodeId,
                    currentState,
                    previousHash
                );
                context.AddEvent(enteredEvent);

                try
                {
                    var result = await ExecuteNodeAsync(node, currentState, ct);

                    if (result is SuccessResult success)
                    {
                        currentState = success.OutputState.WithStep(currentState.Step + 1);
                        await _stateStore.SaveStateAsync(currentState, ct);

                        var exitedEvent = StateEventHelpers.CreateExitedEvent(
                            request.ExecutionId,
                            nodeId,
                            currentState,
                            previousHash
                        );
                        context.AddEvent(exitedEvent);

                        completedNodes.Add(nodeId);

                        var outgoingEdges = request.Graph.GetOutgoingEdges(nodeId);
                        foreach (var edge in outgoingEdges)
                        {
                            if (edge.EvaluateCondition(currentState) && !completedNodes.Contains(edge.TargetNodeId))
                            {
                                pendingNodes.Enqueue(edge.TargetNodeId);
                            }
                        }
                    }
                    else if (result is FailureResult failure)
                    {
                        var errorEvent = StateEventHelpers.CreateErrorEvent(
                            request.ExecutionId,
                            nodeId,
                            currentState,
                            new Exception(failure.Error ?? failure.Reason),
                            previousHash
                        );
                        context.AddEvent(errorEvent);

                        if (!request.Options.ContinueOnError)
                        {
                            cts.Cancel();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var errorEvent = StateEventHelpers.CreateErrorEvent(
                        request.ExecutionId,
                        nodeId,
                        currentState,
                        ex,
                        previousHash
                    );
                    context.AddEvent(errorEvent);

                    if (!request.Options.ContinueOnError)
                    {
                        cts.Cancel();
                    }
                }
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }

        while (!cts.Token.IsCancellationRequested)
        {
            while (pendingNodes.TryDequeue(out var nodeId))
            {
                if (completedNodes.Contains(nodeId))
                    continue;

                if (runningTasks.Count >= request.Options.MaxConcurrency)
                    break;

                var task = ProcessNodeAsync(nodeId, cts.Token);
                runningTasks.Add(task);
            }

            if (runningTasks.Count == 0)
                break;

            var completed = await Task.WhenAny(runningTasks.Where(t => !t.IsCanceled));
            runningTasks.Remove(completed);

            if (completed.IsFaulted && !request.Options.ContinueOnError)
            {
                cts.Cancel();
                break;
            }
        }

        await Task.WhenAll(runningTasks);

        var isCompleted = request.Graph.ExitNodeIds.All(exitId => completedNodes.Contains(exitId));
        currentState = currentState.WithStatus(isCompleted ? WorkflowStatus.Completed : WorkflowStatus.Failed);
        await _stateStore.SaveStateAsync(currentState, cts.Token);

        var finalEvent = isCompleted
            ? StateEventHelpers.CreateCompletedEvent(request.ExecutionId, request.Graph.ExitNodeIds.FirstOrDefault() ?? "", currentState)
            : StateEventHelpers.CreateFailedEvent(request.ExecutionId, "", currentState, "Incomplete execution");

        context.AddEvent(finalEvent);
        yield return finalEvent;
    }

    private async Task<NodeResult> ExecuteNodeAsync(INode node, WorkflowState state, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(node is ILlmClient ? TimeSpan.FromMinutes(2) : TimeSpan.FromSeconds(30));

        return await node.ExecuteAsync(state, cts.Token);
    }

    public async Task<WorkflowState> RunToCompletionAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        WorkflowState finalState = request.InitialState;

        await foreach (var evt in RunAsync(request, cancellationToken))
        {
            finalState = evt.State;
        }

        return finalState;
    }
}
