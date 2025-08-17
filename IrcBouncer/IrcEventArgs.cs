namespace IrcBouncer;

/// <summary>
/// Base class for IRC event arguments containing common message information.
/// </summary>
internal abstract class IrcEventArgsBase : EventArgs
{
    /// <summary>
    /// The original raw IRC message.
    /// </summary>
    public string RawMessage { get; }

    /// <summary>
    /// The parsed IRC message structure.
    /// </summary>
    public IrcMessage Message { get; }

    /// <summary>
    /// The source of the message (nick!user@host or server name).
    /// </summary>
    public string? Source => Message.Prefix;

    protected IrcEventArgsBase(string rawMessage, IrcMessage message)
    {
        RawMessage = rawMessage;
        Message = message;
    }
}

/// <summary>
/// Event arguments for PRIVMSG messages.
/// </summary>
internal sealed class IrcPrivmsgEventArgs : IrcEventArgsBase
{
    /// <summary>
    /// The target of the message (channel or nick).
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// The message text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// True if this is a channel message, false if it's a private message.
    /// </summary>
    public bool IsChannelMessage => Target.StartsWith('#') || Target.StartsWith('&');

    public IrcPrivmsgEventArgs(string rawMessage, IrcMessage message) : base(rawMessage, message)
    {
        Target = message.Parameters.FirstOrDefault() ?? string.Empty;
        Text = message.Trailing ?? string.Empty;
    }
}

/// <summary>
/// Event arguments for NOTICE messages.
/// </summary>
internal sealed class IrcNoticeEventArgs : IrcEventArgsBase
{
    /// <summary>
    /// The target of the notice (channel or nick).
    /// </summary>
    public string Target { get; }

    /// <summary>
    /// The notice text.
    /// </summary>
    public string Text { get; }

    public IrcNoticeEventArgs(string rawMessage, IrcMessage message) : base(rawMessage, message)
    {
        Target = message.Parameters.FirstOrDefault() ?? string.Empty;
        Text = message.Trailing ?? string.Empty;
    }
}

/// <summary>
/// Event arguments for JOIN messages.
/// </summary>
internal sealed class IrcJoinEventArgs : IrcEventArgsBase
{
    /// <summary>
    /// The channel that was joined.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// The nickname of the user who joined.
    /// </summary>
    public string Nick { get; }

    public IrcJoinEventArgs(string rawMessage, IrcMessage message) : base(rawMessage, message)
    {
        Channel = message.Parameters.FirstOrDefault() ?? message.Trailing ?? string.Empty;
        Nick = ExtractNickFromPrefix(message.Prefix);
    }

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
internal sealed class IrcPartEventArgs : IrcEventArgsBase
{
    /// <summary>
    /// The channel that was left.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// The nickname of the user who left.
    /// </summary>
    public string Nick { get; }

    /// <summary>
    /// The part message (reason for leaving).
    /// </summary>
    public string? PartMessage { get; }

    public IrcPartEventArgs(string rawMessage, IrcMessage message) : base(rawMessage, message)
    {
        Channel = message.Parameters.FirstOrDefault() ?? string.Empty;
        Nick = ExtractNickFromPrefix(message.Prefix);
        PartMessage = message.Trailing;
    }

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
internal sealed class IrcErrorEventArgs : IrcEventArgsBase
{
    /// <summary>
    /// The error message from the server.
    /// </summary>
    public string ErrorMessage { get; }

    public IrcErrorEventArgs(string rawMessage, IrcMessage message) : base(rawMessage, message)
    {
        ErrorMessage = message.Trailing ?? string.Empty;
    }
}
