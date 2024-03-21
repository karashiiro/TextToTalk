namespace TextToTalk.Middleware;

/// <summary>
/// Configuration provider for <see cref="ConfiguredRateLimiter"/>.
/// </summary>
public interface IRateLimiterConfigProvider
{
    /// <summary>
    /// Returns whether or not the rate limiter should be enabled.
    /// </summary>
    /// <returns>true if the rate limiter should be enabled; otherwise false.</returns>
    bool ShouldRateLimit();

    /// <summary>
    /// Returns the rate limit, in messages per second.
    /// </summary>
    /// <returns>The rate limit.</returns>
    float MessagesPerSecond();
}