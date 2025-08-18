using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IrcBouncer;

/// <summary>
/// Extensions and utilities for logging with sensitive data redaction.
/// </summary>
public static partial class LoggingExtensions
{
    private static readonly string[] SensitiveCommands = { "PASS", "PRIVMSG NickServ", "NS IDENTIFY" };
    
    [GeneratedRegex(@"^PASS\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PassCommandRegex();
    
    [GeneratedRegex(@"^(PRIVMSG\s+NickServ\s+IDENTIFY\s+)(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NickServIdentifyRegex();
    
    [GeneratedRegex(@"^(NS\s+IDENTIFY\s+)(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NsIdentifyRegex();
    
    /// <summary>
    /// Redacts sensitive information from IRC commands for logging purposes.
    /// </summary>
    /// <param name="message">The IRC message to redact</param>
    /// <returns>The message with sensitive data redacted</returns>
    public static string RedactSensitiveData(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;
            
        // Redact PASS commands
        var passMatch = PassCommandRegex().Match(message);
        if (passMatch.Success)
        {
            return "PASS [REDACTED]";
        }
        
        // Redact NickServ IDENTIFY commands
        var nickServMatch = NickServIdentifyRegex().Match(message);
        if (nickServMatch.Success)
        {
            return nickServMatch.Groups[1].Value + "[REDACTED]";
        }
        
        // Redact NS IDENTIFY commands
        var nsMatch = NsIdentifyRegex().Match(message);
        if (nsMatch.Success)
        {
            return nsMatch.Groups[1].Value + "[REDACTED]";
        }
        
        return message;
    }
    
    /// <summary>
    /// Logs an outgoing IRC message with sensitive data redaction.
    /// </summary>
    public static void LogOutgoingMessage(this ILogger logger, string message)
    {
        var redacted = RedactSensitiveData(message);
        logger.LogInformation("> {Message}", redacted);
    }
    
    /// <summary>
    /// Logs an incoming IRC message with sensitive data redaction.
    /// </summary>
    public static void LogIncomingMessage(this ILogger logger, string message)
    {
        var redacted = RedactSensitiveData(message);
        logger.LogInformation("< {Message}", redacted);
    }
    
    /// <summary>
    /// Checks if a message contains sensitive data that should be redacted.
    /// </summary>
    public static bool ContainsSensitiveData(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;
            
        foreach (var sensitiveCommand in SensitiveCommands)
        {
            if (message.StartsWith(sensitiveCommand, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        
        return false;
    }
}
