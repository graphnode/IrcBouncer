namespace IrcBouncer;

/// <summary>
/// Base class for IRC event arguments containing common message information.
/// </summary>
public abstract class IrcEventArgsBase(string rawMessage, IrcMessage message) : EventArgs
{
    /// <summary>
    /// The original raw IRC message.
    /// </summary>
    public string RawMessage { get; } = rawMessage;

    /// <summary>
    /// The parsed IRC message structure.
    /// </summary>
    public IrcMessage Message { get; } = message;

    /// <summary>
    /// The source of the message (nick!user@host or server name).
    /// </summary>
    public string? Source => Message.Prefix;
}

/// <summary>
/// Event arguments for PRIVMSG messages.
/// </summary>
public sealed class IrcPrivmsgEventArgs(string rawMessage, IrcMessage message) : IrcEventArgsBase(rawMessage, message)
{
    /// <summary>
    /// The target of the message (channel or nick).
    /// </summary>
    public string Target { get; } = message.Parameters.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// The message text.
    /// </summary>
    public string Text { get; } = message.Trailing ?? string.Empty;

    /// <summary>
    /// True if this is a channel message, false if it's a private message.
    /// </summary>
    public bool IsChannelMessage => Target.StartsWith('#') || Target.StartsWith('&');
}

/// <summary>
/// Event arguments for NOTICE messages.
/// </summary>
public sealed class IrcNoticeEventArgs(string rawMessage, IrcMessage message) : IrcEventArgsBase(rawMessage, message)
{
    /// <summary>
    /// The target of the notice (channel or nick).
    /// </summary>
    public string Target { get; } = message.Parameters.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// The notice text.
    /// </summary>
    public string Text { get; } = message.Trailing ?? string.Empty;
}

/// <summary>
/// Event arguments for JOIN messages.
/// </summary>
public sealed class IrcJoinEventArgs(string rawMessage, IrcMessage message) : IrcEventArgsBase(rawMessage, message)
{
    /// <summary>
    /// The channel that was joined.
    /// </summary>
    public string Channel { get; } = message.Parameters.FirstOrDefault() ?? message.Trailing ?? string.Empty;

    /// <summary>
    /// The nickname of the user who joined.
    /// </summary>
    public string Nick { get; } = ExtractNickFromPrefix(message.Prefix);

    private static string ExtractNickFromPrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return string.Empty;

        var exclamationIndex = prefix.IndexOf('!', StringComparison.InvariantCulture);
        return exclamationIndex > 0 ? prefix[..exclamationIndex] : prefix;
    }
}

/// <summary>
/// Event arguments for PART messages.
/// </summary>
public sealed class IrcPartEventArgs(string rawMessage, IrcMessage message) : IrcEventArgsBase(rawMessage, message)
{
    /// <summary>
    /// The channel that was left.
    /// </summary>
    public string Channel { get; } = message.Parameters.FirstOrDefault() ?? string.Empty;

    /// <summary>
    /// The nickname of the user who left.
    /// </summary>
    public string Nick { get; } = ExtractNickFromPrefix(message.Prefix);

    /// <summary>
    /// The part message (reason for leaving).
    /// </summary>
    public string? PartMessage { get; } = message.Trailing;

    private static string ExtractNickFromPrefix(string? prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return string.Empty;

        var exclamationIndex = prefix.IndexOf('!', StringComparison.InvariantCulture);
        return exclamationIndex > 0 ? prefix[..exclamationIndex] : prefix;
    }
}

/// <summary>
/// Event arguments for ERROR messages from the server.
/// </summary>
public sealed class IrcErrorEventArgs(string rawMessage, IrcMessage message) : IrcEventArgsBase(rawMessage, message)
{
    /// <summary>
    /// The error message from the server.
    /// </summary>
    public string ErrorMessage { get; } = message.Trailing ?? string.Empty;
}
