using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace TextToTalk.Middleware;

/// <summary>
/// A <see cref="RateLimiter"/> with configuration.
/// </summary>
/// <param name="config">The plugin's configuration.</param>
public class ConfiguredRateLimiter(IRateLimiterConfigProvider config) : RateLimiter(GetLimitDuration(config))
{
    public bool TryRateLimit(IGameObject speaker)
    {
        return config.ShouldRateLimit() && speaker.ObjectKind is ObjectKind.Player &&
               TryRateLimit(speaker.Name.TextValue);
    }

    private static Func<long> GetLimitDuration(IRateLimiterConfigProvider config)
    {
        return () =>
        {
            var rate = config.MessagesPerSecond();
            if (rate == 0)
            {
                return long.MaxValue;
            }

            return (long)(1000f / rate);
        };
    }
}