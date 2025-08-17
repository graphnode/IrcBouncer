namespace IrcBouncer;

/// <summary>
/// Represents a structured IRC message with prefix, command, parameters, and trailing text.
/// Follows RFC 2812 message format: [:prefix] command [params] [:trailing]
/// </summary>
public sealed class IrcMessage
{
    /// <summary>
    /// Optional prefix (typically server name or nick!user@host).
    /// </summary>
    public string? Prefix { get; set; }

    /// <summary>
    /// IRC command (e.g., "PRIVMSG", "JOIN", "001", etc.).
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// List of parameters (excluding trailing parameter).
    /// </summary>
    public List<string> Parameters { get; set; } = new();

    /// <summary>
    /// Optional trailing parameter (text after the final colon).
    /// </summary>
    public string? Trailing { get; set; }

    /// <summary>
    /// Parses a raw IRC message string into an IrcMessage object.
    /// </summary>
    /// <param name="rawMessage">Raw IRC message string.</param>
    /// <returns>Parsed IrcMessage object.</returns>
    /// <exception cref="ArgumentException">Thrown when the message format is invalid.</exception>
    public static IrcMessage Parse(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
            throw new ArgumentException("IRC message cannot be null or empty.", nameof(rawMessage));

        var message = new IrcMessage();
        var parts = rawMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = 0;

        // Parse prefix (starts with :)
        if (parts.Length > 0 && parts[0].StartsWith(':'))
        {
            message.Prefix = parts[0][1..]; // Remove leading ':'
            index++;
        }

        // Parse command
        if (index >= parts.Length)
            throw new ArgumentException("IRC message must contain a command.", nameof(rawMessage));

        message.Command = parts[index].ToUpperInvariant();
        index++;

        // Parse parameters and trailing
        for (var i = index; i < parts.Length; i++)
        {
            if (parts[i].StartsWith(':'))
            {
                // This is the start of trailing - join all remaining parts
                var trailingStart = rawMessage.IndexOf($" :{parts[i][1..]}", StringComparison.Ordinal);
                if (trailingStart >= 0)
                {
                    message.Trailing = rawMessage[(trailingStart + 2)..]; // Skip " :"
                }
                break;
            }
            else
            {
                message.Parameters.Add(parts[i]);
            }
        }

        return message;
    }

    /// <summary>
    /// Formats the IrcMessage into a raw IRC message string.
    /// </summary>
    /// <returns>Formatted IRC message string.</returns>
    public string Format()
    {
        var parts = new List<string>();

        // Add prefix if present
        if (!string.IsNullOrEmpty(Prefix))
        {
            parts.Add($":{Prefix}");
        }

        // Add command
        if (string.IsNullOrEmpty(Command))
            throw new InvalidOperationException("IRC message must have a command.");

        parts.Add(Command.ToUpperInvariant());

        // Add parameters
        parts.AddRange(Parameters);

        // Add trailing if present
        if (!string.IsNullOrEmpty(Trailing))
        {
            parts.Add($":{Trailing}");
        }

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Returns the formatted IRC message string.
    /// </summary>
    public override string ToString() => Format();

    /// <summary>
    /// Creates a simple IRC message with just a command and optional trailing text.
    /// </summary>
    /// <param name="command">IRC command.</param>
    /// <param name="trailing">Optional trailing text.</param>
    /// <returns>IrcMessage instance.</returns>
    public static IrcMessage Create(string command, string? trailing = null)
    {
        return new IrcMessage
        {
            Command = command,
            Trailing = trailing
        };
    }

    /// <summary>
    /// Creates an IRC message with command, parameters, and optional trailing text.
    /// </summary>
    /// <param name="command">IRC command.</param>
    /// <param name="parameters">Command parameters.</param>
    /// <param name="trailing">Optional trailing text.</param>
    /// <returns>IrcMessage instance.</returns>
    public static IrcMessage Create(string command, IEnumerable<string> parameters, string? trailing = null)
    {
        return new IrcMessage
        {
            Command = command,
            Parameters = parameters.ToList(),
            Trailing = trailing
        };
    }
}
