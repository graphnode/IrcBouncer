using System.Collections.Concurrent;

namespace IrcBouncer.Server;

/// <summary>
/// Thread-safe registry that tracks active downstream sessions.
/// </summary>
public sealed class SessionRegistry
{
    private readonly ConcurrentDictionary<string, IDownstreamSession> _sessions = new();

    /// <summary>
    /// Registers a session by its Id. Returns false if a session with the same Id already exists.
    /// </summary>
    public bool TryAdd(IDownstreamSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return _sessions.TryAdd(session.Id, session);
    }

    /// <summary>
    /// Removes a session by Id.
    /// </summary>
    public bool Remove(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _sessions.TryRemove(sessionId, out _);
    }

    /// <summary>
    /// Attempts to get a session by Id.
    /// </summary>
    public bool TryGet(string sessionId, out IDownstreamSession? session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _sessions.TryGetValue(sessionId, out session);
    }

    /// <summary>
    /// Returns a snapshot of current sessions.
    /// </summary>
    public IReadOnlyCollection<IDownstreamSession> Snapshot()
        => _sessions.Values.ToArray();
}
