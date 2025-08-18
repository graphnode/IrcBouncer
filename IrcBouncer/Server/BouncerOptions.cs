namespace IrcBouncer.Server;

/// <summary>
/// Configuration model for the server-side bouncer (downstream multi-client → single upstream).
/// </summary>
public sealed class BouncerOptions
{
    // Downstream (clients connecting to the bouncer)
    public string BindAddress { get; set; } = "127.0.0.1";
    public int BindPort { get; set; } = 6667;
    public bool DownstreamTls { get; set; } // default false
    public string? ServerCertificatePath { get; set; }
    public string? ServerCertificatePassword { get; set; }

    // Upstream (bouncer connecting to real IRC server)
    public string UpstreamHost { get; set; } = "irc.libera.chat";
    public int UpstreamPort { get; set; } = 6697;
    public bool UpstreamTls { get; set; } = true;

    // Auth and limits
    public string? SharedSecret { get; set; }
    public int MaxSessions { get; set; } = 100;

    /// <summary>
    /// Basic per-session message rate (messages per second). 0 or less disables limiting here (protocol layer may still limit).
    /// </summary>
    public int SessionRatePerSecond { get; set; } = 0;

    /// <summary>
    /// Validates the options; returns false and fills error if invalid.
    /// </summary>
    public bool TryValidate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(BindAddress))
        {
            error = "BindAddress cannot be empty";
            return false;
        }
        if (BindPort <= 0 || BindPort > 65535)
        {
            error = "BindPort must be between 1 and 65535";
            return false;
        }
        if (string.IsNullOrWhiteSpace(UpstreamHost))
        {
            error = "UpstreamHost cannot be empty";
            return false;
        }
        if (UpstreamPort <= 0 || UpstreamPort > 65535)
        {
            error = "UpstreamPort must be between 1 and 65535";
            return false;
        }
        if (DownstreamTls)
        {
            if (string.IsNullOrWhiteSpace(ServerCertificatePath))
            {
                error = "ServerCertificatePath is required when DownstreamTls is enabled";
                return false;
            }
        }
        if (MaxSessions <= 0)
        {
            error = "MaxSessions must be greater than 0";
            return false;
        }
        error = null;
        return true;
    }
}
