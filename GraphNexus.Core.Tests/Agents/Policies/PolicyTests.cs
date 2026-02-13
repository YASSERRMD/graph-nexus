using GraphNexus.Agents.Policies;
using GraphNexus.Primitives;
using Xunit;

namespace GraphNexus.Core.Tests.Agents.Policies;

public class PolicyTests
{
    [Fact]
    public async Task RbacPolicy_WithValidRole_ShouldAllow()
    {
        var permissions = new Dictionary<string, IReadOnlyList<string>>
        {
            ["admin"] = ["read", "write", "delete"],
            ["user"] = ["read"]
        };
        var policy = new RbacPolicy(permissions, "user");
        var message = Message.Create("user", "Test message");

        var result = await policy.ValidateAsync(message);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task RbacPolicy_WithUnknownRole_ShouldDeny()
    {
        var permissions = new Dictionary<string, IReadOnlyList<string>>
        {
            ["admin"] = ["read", "write"]
        };
        var policy = new RbacPolicy(permissions, "user");
        var message = Message.Create("guest", "Test message");

        var result = await policy.ValidateAsync(message);

        Assert.False(result.IsAllowed);
        Assert.Contains("guest", result.Reason);
    }

    [Fact]
    public async Task ContentFilterPolicy_WithBlockedPattern_ShouldDeny()
    {
        var blockedPatterns = new List<string> { "badword", "forbidden" };
        var policy = new ContentFilterPolicy(blockedPatterns);
        var message = Message.Create("user", "This contains badword in it");

        var result = await policy.ValidateAsync(message);

        Assert.False(result.IsAllowed);
        Assert.Contains("badword", result.Reason);
    }

    [Fact]
    public async Task ContentFilterPolicy_WithCleanContent_ShouldAllow()
    {
        var policy = new ContentFilterPolicy();
        var message = Message.Create("user", "This is a clean message");

        var result = await policy.ValidateAsync(message);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task RateLimitPolicy_UnderLimit_ShouldAllow()
    {
        var policy = new RateLimitPolicy(maxRequestsPerMinute: 10);

        for (int i = 0; i < 5; i++)
        {
            var result = await policy.ValidateAsync(Message.Create("user", $"Message {i}"));
            Assert.True(result.IsAllowed);
        }
    }

    [Fact]
    public async Task RateLimitPolicy_OverLimit_ShouldDeny()
    {
        var policy = new RateLimitPolicy(maxRequestsPerMinute: 3);

        for (int i = 0; i < 3; i++)
        {
            await policy.ValidateAsync(Message.Create("user", $"Message {i}"));
        }

        var result = await policy.ValidateAsync(Message.Create("user", "One more"));

        Assert.False(result.IsAllowed);
        Assert.Contains("Rate limit exceeded", result.Reason);
    }

    [Fact]
    public async Task PolicyChain_WhenAllPoliciesPass_ShouldAllow()
    {
        var chain = new PolicyChain(
            new ContentFilterPolicy(),
            new RbacPolicy(new Dictionary<string, IReadOnlyList<string>> { ["user"] = ["read"] })
        );
        var message = Message.Create("user", "Clean message");

        var result = await chain.ValidateAsync(message);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task PolicyChain_WhenOnePolicyFails_ShouldDeny()
    {
        var chain = new PolicyChain(
            new ContentFilterPolicy(new List<string> { "bad" }),
            new RbacPolicy(new Dictionary<string, IReadOnlyList<string>> { ["user"] = ["read"] })
        );
        var message = Message.Create("user", "This is bad content");

        var result = await chain.ValidateAsync(message);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void PolicyResult_Allowed_ShouldReturnAllowedResult()
    {
        var result = PolicyResult.Allowed();

        Assert.True(result.IsAllowed);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void PolicyResult_Denied_ShouldReturnDeniedResult()
    {
        var result = PolicyResult.Denied("Test reason");

        Assert.False(result.IsAllowed);
        Assert.Equal("Test reason", result.Reason);
    }
}
