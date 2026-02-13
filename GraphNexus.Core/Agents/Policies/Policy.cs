using GraphNexus.Primitives;

namespace GraphNexus.Agents.Policies;

public interface IPolicy
{
    string Name { get; }
    string Description { get; }
    Task<PolicyResult> ValidateAsync(Message message, CancellationToken cancellationToken = default);
}

public sealed record PolicyResult
{
    public bool IsAllowed { get; init; }
    public string? Reason { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }

    public static PolicyResult Allowed() => new() { IsAllowed = true };
    public static PolicyResult Denied(string reason) => new() { IsAllowed = false, Reason = reason };
    public static PolicyResult Denied(string reason, IReadOnlyDictionary<string, object>? metadata) =>
        new() { IsAllowed = false, Reason = reason, Metadata = metadata };
}

public sealed class RbacPolicy : IPolicy
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _rolePermissions;
    private readonly string _defaultRole;

    public string Name => "RBAC Policy";
    public string Description => "Role-based access control for agent actions";

    public RbacPolicy(IReadOnlyDictionary<string, IReadOnlyList<string>> rolePermissions, string defaultRole = "user")
    {
        _rolePermissions = rolePermissions;
        _defaultRole = defaultRole;
    }

    public Task<PolicyResult> ValidateAsync(Message message, CancellationToken cancellationToken = default)
    {
        var role = message.Name ?? _defaultRole;

        if (!_rolePermissions.TryGetValue(role, out var permissions))
        {
            return Task.FromResult(PolicyResult.Denied($"No permissions defined for role: {role}"));
        }

        return Task.FromResult(PolicyResult.Allowed());
    }
}

public sealed class ContentFilterPolicy : IPolicy
{
    private readonly IReadOnlyList<string> _blockedPatterns;
    private readonly bool _strictMode;

    public string Name => "Content Filter Policy";
    public string Description => "Filters blocked content patterns";

    public ContentFilterPolicy(IReadOnlyList<string>? blockedPatterns = null, bool strictMode = false)
    {
        _blockedPatterns = blockedPatterns ?? GetDefaultBlockedPatterns();
        _strictMode = strictMode;
    }

    public Task<PolicyResult> ValidateAsync(Message message, CancellationToken cancellationToken = default)
    {
        var content = message.Content.ToLowerInvariant();

        foreach (var pattern in _blockedPatterns)
        {
            if (content.Contains(pattern.ToLowerInvariant()))
            {
                return Task.FromResult(PolicyResult.Denied(
                    $"Content contains blocked pattern: {pattern}",
                    new Dictionary<string, object> { ["pattern"] = pattern }
                ));
            }
        }

        return Task.FromResult(PolicyResult.Allowed());
    }

    private static List<string> GetDefaultBlockedPatterns()
    {
        return new List<string> { "malicious", "harmful", "illegal" };
    }
}

public sealed class RateLimitPolicy : IPolicy
{
    private readonly int _maxRequestsPerMinute;
    private readonly Dictionary<string, List<DateTimeOffset>> _requestTimestamps = [];
    private readonly object _lock = new();

    public string Name => "Rate Limit Policy";
    public string Description => "Limits requests per minute";

    public RateLimitPolicy(int maxRequestsPerMinute = 60)
    {
        _maxRequestsPerMinute = maxRequestsPerMinute;
    }

    public Task<PolicyResult> ValidateAsync(Message message, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-1);

        lock (_lock)
        {
            var key = message.Name ?? "anonymous";

            if (!_requestTimestamps.TryGetValue(key, out var timestamps))
            {
                timestamps = [];
                _requestTimestamps[key] = timestamps;
            }

            timestamps.RemoveAll(t => t < windowStart);

            if (timestamps.Count >= _maxRequestsPerMinute)
            {
                return Task.FromResult(PolicyResult.Denied(
                    $"Rate limit exceeded: {timestamps.Count} requests in the last minute",
                    new Dictionary<string, object> { ["count"] = timestamps.Count, ["limit"] = _maxRequestsPerMinute }
                ));
            }

            timestamps.Add(now);
        }

        return Task.FromResult(PolicyResult.Allowed());
    }
}

public sealed class PolicyChain : IPolicy
{
    private readonly IReadOnlyList<IPolicy> _policies;

    public string Name => "Policy Chain";
    public string Description => "Chains multiple policies together";

    public PolicyChain(params IPolicy[] policies)
    {
        _policies = policies;
    }

    public async Task<PolicyResult> ValidateAsync(Message message, CancellationToken cancellationToken = default)
    {
        foreach (var policy in _policies)
        {
            var result = await policy.ValidateAsync(message, cancellationToken);
            if (!result.IsAllowed)
            {
                return result;
            }
        }

        return PolicyResult.Allowed();
    }
}
