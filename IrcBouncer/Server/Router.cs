using System.Collections.Concurrent;

namespace IrcBouncer.Server;

/// <summary>
/// Minimal router implementation that fans in lines from sessions to a single upstream
/// and fans out lines from upstream to all sessions. Upstream writes are serialized.
/// </summary>
public sealed class Router : IRouter, IDisposable
{
    private readonly IUpstreamConnection _upstream;
    private readonly ConcurrentDictionary<string, IDownstreamSession> _sessions = new();
    private readonly SemaphoreSlim _upstreamWriteLock = new(1, 1);
    private bool _disposed;

    public Router(IUpstreamConnection upstream)
    {
        _upstream = upstream ?? throw new ArgumentNullException(nameof(upstream));
    }

    public void AddSession(IDownstreamSession session)
    {
        ThrowIfDisposed();
        if (session is null) throw new ArgumentNullException(nameof(session));
        _sessions[session.Id] = session;
    }

    public void RemoveSession(string sessionId)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(sessionId));
        _sessions.TryRemove(sessionId, out _);
    }

    public async Task FanOutAsync(string upstreamLine, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (upstreamLine is null) throw new ArgumentNullException(nameof(upstreamLine));

        // Snapshot to avoid enumeration issues during concurrent modifications.
        var sessions = _sessions.Values.ToArray();
        var tasks = new List<Task>(sessions.Length);
        foreach (var s in sessions)
        {
            tasks.Add(SendSafeAsync(s, upstreamLine, cancellationToken));
        }
        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch
        {
            // Ignore per-session send failures; sessions may be removed elsewhere on disconnect.
        }
    }

    public async Task FanInAsync(string lineFromSession, string sessionId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (lineFromSession is null) throw new ArgumentNullException(nameof(lineFromSession));
        if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(sessionId));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await _upstreamWriteLock.WaitAsync(linked.Token).ConfigureAwait(false);
            await _upstream.SendAsync(lineFromSession, linked.Token).ConfigureAwait(false);
        }
        finally
        {
            _upstreamWriteLock.Release();
        }
    }

    private static async Task SendSafeAsync(IDownstreamSession session, string line, CancellationToken token)
    {
        try
        {
            await session.SendAsync(line, token).ConfigureAwait(false);
        }
        catch
        {
            // ignore per-session send errors
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(Router));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _upstreamWriteLock.Dispose();
        _sessions.Clear();
    }
}
