using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GraphNexus.Primitives;

namespace GraphNexus.Execution;

public static class StateEventHelpers
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static JsonPatch.PatchDocument CreatePatch(WorkflowState oldState, WorkflowState newState)
    {
        var patch = new JsonPatch.PatchDocument();

        if (oldState.Step != newState.Step)
        {
            patch.Add(new JsonPatch.JsonPatchOperation("add", "/step", newState.Step));
        }

        if (oldState.Status != newState.Status)
        {
            patch.Add(new JsonPatch.JsonPatchOperation("add", "/status", newState.Status.ToString()));
        }

        if (oldState.CurrentNodeId != newState.CurrentNodeId)
        {
            if (newState.CurrentNodeId != null)
                patch.Add(new JsonPatch.JsonPatchOperation("add", "/currentNodeId", newState.CurrentNodeId));
        }

        foreach (var (key, newValue) in newState.Data)
        {
            if (!oldState.Data.TryGetValue(key, out var oldValue) || !Equals(oldValue, newValue))
            {
                patch.Add(new JsonPatch.JsonPatchOperation("add", $"/data/{key}", newValue));
            }
        }

        if (newState.Messages.Count > oldState.Messages.Count)
        {
            var newMessages = newState.Messages.Skip(oldState.Messages.Count).ToList();
            patch.Add(new JsonPatch.JsonPatchOperation("add", "/messages", newMessages));
        }

        if (!string.IsNullOrEmpty(oldState.Error) || !string.IsNullOrEmpty(newState.Error))
        {
            patch.Add(new JsonPatch.JsonPatchOperation("add", "/error", newState.Error));
        }

        return patch;
    }

    public static string ComputeStateHash(WorkflowState state)
    {
        var json = JsonSerializer.Serialize(state, SerializerOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string ComputeEventHash(StateEvent evt)
    {
        var json = JsonSerializer.Serialize(evt, StateEventSerializerOptions.Default);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool ValidateHash(WorkflowState state, string expectedHash)
    {
        var computed = ComputeStateHash(state);
        return computed == expectedHash;
    }

    public static NodeEnteredEvent CreateEnteredEvent(string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        return new NodeEnteredEvent(
            Guid.NewGuid().ToString(),
            executionId,
            nodeId,
            state,
            previousHash
        );
    }

    public static NodeExitedEvent CreateExitedEvent(string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        return new NodeExitedEvent(
            Guid.NewGuid().ToString(),
            executionId,
            nodeId,
            state,
            previousHash
        );
    }

    public static NodeErrorEvent CreateErrorEvent(string executionId, string nodeId, WorkflowState state, Exception ex, string? previousHash = null)
    {
        return new NodeErrorEvent(
            Guid.NewGuid().ToString(),
            executionId,
            nodeId,
            state,
            ex.Message,
            ex.StackTrace ?? "",
            previousHash
        );
    }

    public static WorkflowCompletedEvent CreateCompletedEvent(string executionId, string nodeId, WorkflowState state, string? previousHash = null)
    {
        return new WorkflowCompletedEvent(
            Guid.NewGuid().ToString(),
            executionId,
            nodeId,
            state,
            previousHash
        );
    }

    public static WorkflowFailedEvent CreateFailedEvent(string executionId, string nodeId, WorkflowState state, string error, string? previousHash = null)
    {
        return new WorkflowFailedEvent(
            Guid.NewGuid().ToString(),
            executionId,
            nodeId,
            state,
            error,
            previousHash
        );
    }
}

namespace JsonPatch;

public class PatchDocument : List<JsonPatchOperation>
{
    public void Add(JsonPatchOperation operation)
    {
        Add(operation);
    }
}

public class JsonPatchOperation
{
    public string Op { get; }
    public string Path { get; }
    public object? Value { get; }

    public JsonPatchOperation(string op, string path, object? value)
    {
        Op = op;
        Path = path;
        Value = value;
    }
}
