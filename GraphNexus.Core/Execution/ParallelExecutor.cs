using System.Runtime.ExceptionServices;
using GraphNexus.Execution.Resilience;
using GraphNexus.Graph;
using GraphNexus.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public sealed class ParallelExecutor
{
    private readonly IStateStore _stateStore;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreakerRegistry _circuitBreakerRegistry;
    private readonly ILogger? _logger;

    public ParallelExecutor(
        IStateStore stateStore,
        int maxConcurrency = 4,
        RetryPolicy? retryPolicy = null,
        CircuitBreakerRegistry? circuitBreakerRegistry = null,
        ILogger? logger = null)
    {
        _stateStore = stateStore;
        _concurrencySemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _retryPolicy = retryPolicy ?? new RetryPolicy();
        _circuitBreakerRegistry = circuitBreakerRegistry ?? new CircuitBreakerRegistry();
        _logger = logger;
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
                    var result = await ExecuteNodeWithRetryAsync(node, currentState, ct, request.Options);

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

                        _logger?.LogError("Node {NodeId} failed: {Error}", nodeId, failure.Error ?? failure.Reason);

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

                    _logger?.LogError(ex, "Node {NodeId} threw exception", nodeId);

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

    private async Task<NodeResult> ExecuteNodeWithRetryAsync(
        INode node,
        WorkflowState state,
        CancellationToken cancellationToken,
        ExecutionOptions options)
    {
        var isLlmNode = node is LlmNode;
        var timeout = isLlmNode ? options.LlmNodeTimeout : options.NodeTimeout;

        if (options.EnableRetry)
        {
            return await _retryPolicy.ExecuteAsync(
                async (attempt, ct) =>
                {
                    var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    nodeCts.CancelAfter(timeout);

                    try
                    {
                        var result = await node.ExecuteAsync(state, nodeCts.Token);

                        if (options.EnableCircuitBreaker && isLlmNode)
                        {
                            var circuitBreaker = _circuitBreakerRegistry.GetOrCreate("llm");
                            return await circuitBreaker.ExecuteAsync(async ct2 =>
                            {
                                var result2 = await node.ExecuteAsync(state, ct2);

                                if (result2 is FailureResult failure && IsTransientFailure(failure))
                                {
                                    throw new TransientFailureException(failure.Error ?? failure.Reason);
                                }

                                return result2;
                            }, ct);
                        }

                        return result;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        throw new TimeoutException($"Node execution timed out after {timeout}");
                    }
                    catch (TransientFailureException)
                    {
                        throw;
                    }
                    catch (Exception ex) when (IsTransientException(ex))
                    {
                        _logger?.LogWarning(ex, "Transient error executing node {NodeId}, attempt {Attempt}", node.Id, attempt + 1);
                        throw;
                    }
                    finally
                    {
                        nodeCts.Dispose();
                    }
                },
                cancellationToken,
                attempt => _logger?.LogInformation("Retrying node {NodeId}, attempt {Attempt}/{MaxRetries}",
                    node.Id, attempt.AttemptNumber, attempt.MaxRetries));
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return await node.ExecuteAsync(state, cts.Token);
    }

    private static bool IsTransientFailure(FailureResult failure)
    {
        var error = failure.Error ?? failure.Reason;
        return error != null && (
            error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("429", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("503", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("temporary", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is TimeoutException ||
               ex is HttpRequestException ||
               ex is IOException ||
               (ex is OperationCanceledException && ex.Message.Contains("timeout"));
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

public sealed class TransientFailureException : Exception
{
    public TransientFailureException(string message) : base(message) { }
}

public interface ILogger
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogWarning(Exception ex, string message, params object[] args);
    void LogError(string message, params object[] args);
    void LogError(Exception ex, string message, params object[] args);
}

public sealed class NoOpLogger : ILogger
{
    public static readonly NoOpLogger Instance = new();
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
    public void LogWarning(Exception ex, string message, params object[] args) { }
    public void LogError(string message, params object[] args) { }
    public void LogError(Exception ex, string message, params object[] args) { }
}
