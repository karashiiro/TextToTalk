using System.Threading.Tasks;
using TextToTalk.Middleware;
using Xunit;

namespace TextToTalk.Tests;

public class RateLimiterTests
{
    [Fact]
    public async Task RateLimiter_Works_WhenNotLimited()
    {
        const string speaker = "12345";
        using var limiter = new RateLimiter(() => 200);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(500);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(500);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(500);
        Assert.False(limiter.TryRateLimit(speaker));
    }

    [Fact]
    public async Task RateLimiter_Works_WhenLimited()
    {
        const string speaker = "12345";
        using var limiter = new RateLimiter(() => 200);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(50);
        Assert.True(limiter.TryRateLimit(speaker));
        await Task.Delay(50);
        Assert.True(limiter.TryRateLimit(speaker));
        await Task.Delay(50);
        Assert.True(limiter.TryRateLimit(speaker));
    }

    [Fact]
    public async Task RateLimiter_Works_Mixed()
    {
        const string speaker = "12345";
        using var limiter = new RateLimiter(() => 200);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(500);
        Assert.False(limiter.TryRateLimit(speaker));
        await Task.Delay(50);
        Assert.True(limiter.TryRateLimit(speaker));
    }
}