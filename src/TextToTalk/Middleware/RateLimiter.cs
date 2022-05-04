using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TextToTalk.Middleware
{
    public sealed class RateLimiter : IDisposable
    {
        private readonly Dictionary<string, long> times = new();
        private readonly Func<long> getLimitMs;
        private readonly CancellationTokenSource tokenSource = new();

        public RateLimiter(Func<long> getLimitMs)
        {
            this.getLimitMs = getLimitMs;
            _ = Cleanup(this.tokenSource.Token);
        }

        /// <summary>
        /// Checks if we should rate-limit the current speaker. Returns true if we should
        /// rate-limit this speaker, or false if we shouldn't.
        /// </summary>
        /// <param name="speaker"></param>
        /// <returns></returns>
        public bool TryRateLimit(string speaker)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var limit = this.getLimitMs.Invoke();

            lock (this.times)
            {
                if (!this.times.TryGetValue(speaker, out var t))
                {
                    // No timestamp; create it and leave
                    t = now;
                    this.times.Add(speaker, t);
                    return false;
                }

                // Check if we should skip this or not, and update the timestamp if not
                var shouldLimit = now - t <= limit;
                if (!shouldLimit)
                {
                    this.times[speaker] = now;
                }
                
                return shouldLimit;
            }
        }

        private async Task Cleanup(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var limit = this.getLimitMs.Invoke();
                var toRemove = new List<string>();

                lock (this.times)
                {
                    foreach (var (speaker, t) in this.times)
                    {
                        if (now - t > limit)
                        {
                            toRemove.Add(speaker);
                        }
                    }

                    foreach (var speaker in toRemove)
                    {
                        this.times.Remove(speaker);
                    }
                }

                await Task.Delay(3000, token);
            }
        }

        public void Dispose()
        {
            tokenSource.Dispose();
        }
    }
}