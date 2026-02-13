using System.Threading.Channels;
using GraphNexus.Execution;
using GraphNexus.Graph;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public sealed class ParallelExecutor
{
    private readonly IStateStore _stateStore;

    public ParallelExecutor(IStateStore stateStore)
    {
        _stateStore = stateStore;
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

        var completedNodes = new HashSet<string>();
        var pendingNodes = new Queue<string>();

        if (!string.IsNullOrEmpty(request.Graph.EntryNodeId))
        {
            pendingNodes.Enqueue(request.Graph.EntryNodeId);
        }

        while (pendingNodes.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            var nodeId = pendingNodes.Dequeue();

            if (completedNodes.Contains(nodeId))
                continue;

            if (!request.Graph.Nodes.TryGetValue(nodeId, out var node))
                continue;

            var previousHash = StateEventHelpers.ComputeStateHash(currentState);

            var enteredEvent = StateEventHelpers.CreateEnteredEvent(
                request.ExecutionId,
                nodeId,
                currentState,
                previousHash
            );
            context.AddEvent(enteredEvent);
            yield return enteredEvent;

            try
            {
                var result = await ExecuteNodeAsync(node, currentState, cancellationToken);

                if (result is SuccessResult success)
                {
                    currentState = success.OutputState.WithStep(currentState.Step + 1);
                    await _stateStore.SaveStateAsync(currentState, cancellationToken);

                    var exitedEvent = StateEventHelpers.CreateExitedEvent(
                        request.ExecutionId,
                        nodeId,
                        currentState,
                        previousHash
                    );
                    context.AddEvent(exitedEvent);
                    yield return exitedEvent;

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
                    yield return errorEvent;

                    if (!request.Options.ContinueOnError)
                    {
                        currentState = currentState.WithStatus(WorkflowStatus.Failed, failure.Error ?? failure.Reason);
                        await _stateStore.SaveStateAsync(currentState, cancellationToken);

                        var failedEvent = StateEventHelpers.CreateFailedEvent(
                            request.ExecutionId,
                            nodeId,
                            currentState,
                            failure.Reason
                        );
                        context.AddEvent(failedEvent);
                        yield return failedEvent;

                        yield break;
                    }
                }
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
                yield return errorEvent;

                if (!request.Options.ContinueOnError)
                {
                    currentState = currentState.WithStatus(WorkflowStatus.Failed, ex.Message);
                    await _stateStore.SaveStateAsync(currentState, cancellationToken);

                    var failedEvent = StateEventHelpers.CreateFailedEvent(
                        request.ExecutionId,
                        nodeId,
                        currentState,
                        ex.Message
                    );
                    context.AddEvent(failedEvent);
                    yield return failedEvent;

                    yield break;
                }
            }
        }

        var isCompleted = request.Graph.ExitNodeIds.All(exitId => completedNodes.Contains(exitId));
        currentState = currentState.WithStatus(isCompleted ? WorkflowStatus.Completed : WorkflowStatus.Failed);
        await _stateStore.SaveStateAsync(currentState, cancellationToken);

        var finalEvent = isCompleted
            ? StateEventHelpers.CreateCompletedEvent(request.ExecutionId, request.Graph.ExitNodeIds.FirstOrDefault() ?? "", currentState)
            : StateEventHelpers.CreateFailedEvent(request.ExecutionId, "", currentState, "Incomplete execution");

        context.AddEvent(finalEvent);
        yield return finalEvent;
    }

    private async Task<NodeResult> ExecuteNodeAsync(INode node, WorkflowState state, CancellationToken cancellationToken)
    {
        return await node.ExecuteAsync(state, cancellationToken);
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
