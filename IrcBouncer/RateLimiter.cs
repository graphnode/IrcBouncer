namespace IrcBouncer;

/// <summary>
/// Configuration options for IRC rate limiting.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Maximum number of messages per time window. Default: 5.
    /// </summary>
    public int MaxMessages { get; set; } = 5;

    /// <summary>
    /// Time window in milliseconds for rate limiting. Default: 2000 (2 seconds).
    /// </summary>
    public int WindowMs { get; set; } = 2000;

    /// <summary>
    /// Whether rate limiting is enabled. Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Simple token bucket rate limiter for IRC messages.
/// Prevents server flood-kick by limiting outgoing message rate.
/// </summary>
internal sealed class RateLimiter(RateLimitOptions options) : IDisposable
{
    private readonly RateLimitOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Queue<DateTime> _messageTimestamps = new();

    /// <summary>
    /// Waits until it's safe to send a message according to rate limiting rules.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait.</param>
    /// <returns>Task that completes when it's safe to send.</returns>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMilliseconds(-_options.WindowMs);

            // Remove timestamps outside the current window
            while (_messageTimestamps.Count > 0 && _messageTimestamps.Peek() < windowStart)
            {
                _messageTimestamps.Dequeue();
            }

            // If we're at the limit, wait until we can send
            if (_messageTimestamps.Count >= _options.MaxMessages)
            {
                var oldestTimestamp = _messageTimestamps.Peek();
                var waitTime = oldestTimestamp.AddMilliseconds(_options.WindowMs) - now;

                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime, cancellationToken).ConfigureAwait(false);

                    // Clean up again after waiting
                    var newNow = DateTime.UtcNow;
                    var newWindowStart = newNow.AddMilliseconds(-_options.WindowMs);
                    while (_messageTimestamps.Count > 0 && _messageTimestamps.Peek() < newWindowStart)
                    {
                        _messageTimestamps.Dequeue();
                    }
                }
            }

            // Record this message timestamp
            _messageTimestamps.Enqueue(now);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Gets the current number of messages in the rate limiting window.
    /// </summary>
    public async Task<int> GetCurrentCountAsync()
    {
        if (!_options.Enabled)
            return 0;

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            var windowStart = now.AddMilliseconds(-_options.WindowMs);

            // Clean up old timestamps
            while (_messageTimestamps.Count > 0 && _messageTimestamps.Peek() < windowStart)
            {
                _messageTimestamps.Dequeue();
            }

            return _messageTimestamps.Count;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Dispose() => _semaphore.Dispose();
}
