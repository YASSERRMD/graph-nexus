using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraphNexus.Primitives;

public abstract record NodeResult
{
    public required string NodeId { get; init; }
    public required string ExecutionId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string? Error { get; init; }

    public string ComputeHash()
    {
        var json = JsonSerializer.Serialize(this, NodeResultSerializerOptions.Default);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record SuccessResult : NodeResult
{
    public required WorkflowState OutputState { get; init; }

    public SuccessResult(string nodeId, string executionId, WorkflowState outputState, string? error = null)
    {
        NodeId = nodeId;
        ExecutionId = executionId;
        OutputState = outputState;
        Error = error;
    }
}

public sealed record FailureResult : NodeResult
{
    public required string Reason { get; init; }

    public FailureResult(string nodeId, string executionId, string reason, string? error = null)
    {
        NodeId = nodeId;
        ExecutionId = executionId;
        Reason = reason;
        Error = error;
    }
}

public sealed record SkippedResult : NodeResult
{
    public required string Reason { get; init; }

    public SkippedResult(string nodeId, string executionId, string reason)
    {
        NodeId = nodeId;
        ExecutionId = executionId;
        Reason = reason;
    }
}

public static class NodeResultSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new NodeResultJsonConverter() }
    };
}

public class NodeResultJsonConverter : JsonConverter<NodeResult>
{
    public override NodeResult? Read(ref Utf8JsonReader reader, Type typeToRead, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("nodeId", out _))
        {
            var kind = root.TryGetProperty("reason", out _) ? "skipped" : 
                       root.TryGetProperty("outputState", out _) ? "success" : "failure";

            return kind switch
            {
                "success" => JsonSerializer.Deserialize<SuccessResult>(root.GetRawText(), options),
                "failure" => JsonSerializer.Deserialize<FailureResult>(root.GetRawText(), options),
                "skipped" => JsonSerializer.Deserialize<SkippedResult>(root.GetRawText(), options),
                _ => throw new JsonException("Unknown NodeResult type")
            };
        }

        throw new JsonException("Invalid NodeResult");
    }

    public override void Write(Utf8JsonWriter writer, NodeResult value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
