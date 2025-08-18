using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using IrcBouncer.Server;

namespace IrcBouncer;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
[SuppressMessage("Design", "CA1031:Do not catch general exception types")]
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var serverOption = new Option<string>("--server", "-s")
        {
            Description = "IRC server host",
            Arity = ArgumentArity.ExactlyOne
        };

        var portOption = new Option<int?>("--port", "-p")
        {
            Description = "IRC server port",
            Arity = ArgumentArity.ExactlyOne
        };

        var tlsOption = new Option<bool>("--tls") { Description = "Use TLS to connect (default)" };
        var noTlsOption = new Option<bool>("--notls") { Description = "Do not use TLS to connect" };

        var nickOption = new Option<string>("--nick", "-n")
        {
            Description = "Nickname to use",
            Arity = ArgumentArity.ExactlyOne
        };

        var userOption = new Option<string>("--user", "-u")
        {
            Description = "Username to use",
            Arity = ArgumentArity.ExactlyOne
        };

        var realOption = new Option<string>("--real", "-r")
        {
            Description = "Real name to use",
            Arity = ArgumentArity.ExactlyOne
        };

        var passOption = new Option<string?>("--pass")
        {
            Description = "Server password (optional)",
            Arity = ArgumentArity.ExactlyOne
        };

        var logLevelOption = new Option<LogLevel?>("--log-level")
        {
            Description = "Log level (Trace, Debug, Information, Warning, Error, Critical, None)",
            Arity = ArgumentArity.ExactlyOne
        };


        // Server-side bouncer command (Phase 2 scaffolding)
        var bindAddressOption = new Option<string?>("--bind-address") { Description = "Downstream bind address (default 127.0.0.1)", Arity = ArgumentArity.ExactlyOne };
        var bindPortOption = new Option<int?>("--bind-port") { Description = "Downstream bind port (default 6667)", Arity = ArgumentArity.ExactlyOne };
        var downstreamTlsOption = new Option<bool>("--downstream-tls") { Description = "Enable TLS for downstream clients (requires cert)" };
        var certPathOption = new Option<string?>("--cert-path") { Description = "Path to server certificate (when downstream TLS is enabled)", Arity = ArgumentArity.ExactlyOne };
        var certPasswordOption = new Option<string?>("--cert-password") { Description = "Password for server certificate (optional)", Arity = ArgumentArity.ExactlyOne };

        var upstreamHostOption = new Option<string?>("--upstream-host") { Description = "Upstream IRC host (default irc.libera.chat)", Arity = ArgumentArity.ExactlyOne };
        var upstreamPortOption = new Option<int?>("--upstream-port") { Description = "Upstream IRC port (default 6697)", Arity = ArgumentArity.ExactlyOne };
        var upstreamTlsOption = new Option<bool>("--upstream-tls") { Description = "Use TLS to connect upstream (default)" };
        var upstreamNoTlsOption = new Option<bool>("--upstream-notls") { Description = "Do not use TLS to connect upstream" };

        var sharedSecretOption = new Option<string?>("--secret") { Description = "Shared secret required from downstream clients via PASS", Arity = ArgumentArity.ExactlyOne };
        var maxSessionsOption = new Option<int?>("--max-sessions") { Description = "Maximum concurrent downstream sessions (default 100)", Arity = ArgumentArity.ExactlyOne };
        var sessionRateOption = new Option<int?>("--session-rate") { Description = "Per-session message rate limit (msgs/sec, default disabled)", Arity = ArgumentArity.ExactlyOne };
        var serveLogLevelOption = new Option<LogLevel?>("--log-level") { Description = "Log level for server mode", Arity = ArgumentArity.ExactlyOne };

        var serve = new Command("serve", "Run the IRC bouncer server (scaffolding)")
        {
            bindAddressOption,
            bindPortOption,
            downstreamTlsOption,
            certPathOption,
            certPasswordOption,
            upstreamHostOption,
            upstreamPortOption,
            upstreamTlsOption,
            upstreamNoTlsOption,
            sharedSecretOption,
            maxSessionsOption,
            sessionRateOption,
            serveLogLevelOption
        };

        serve.SetAction(async (result, _) =>
        {
            var options = new BouncerOptions();

            var bindAddress = result.GetValue(bindAddressOption);
            var bindPort = result.GetValue(bindPortOption);
            var downstreamTls = result.GetValue(downstreamTlsOption);
            var certPath = result.GetValue(certPathOption);
            var certPassword = result.GetValue(certPasswordOption);
            var upstreamHost = result.GetValue(upstreamHostOption);
            var upstreamPort = result.GetValue(upstreamPortOption);
            var upstreamTls = result.GetValue(upstreamTlsOption);
            var upstreamNoTls = result.GetValue(upstreamNoTlsOption);
            var secret = result.GetValue(sharedSecretOption);
            var maxSessions = result.GetValue(maxSessionsOption);
            var sessionRate = result.GetValue(sessionRateOption);
            var logLevel = result.GetValue(serveLogLevelOption) ?? LogLevel.Information;

            if (upstreamTls && upstreamNoTls)
            {
                await Console.Error.WriteLineAsync("Options --upstream-tls and --upstream-notls are mutually exclusive.").ConfigureAwait(false);
                Environment.ExitCode = 2;
                return;
            }

            if (!string.IsNullOrWhiteSpace(bindAddress)) options.BindAddress = bindAddress;
            if (bindPort is > 0) options.BindPort = bindPort.Value;
            options.DownstreamTls = downstreamTls;
            if (!string.IsNullOrWhiteSpace(certPath)) options.ServerCertificatePath = certPath;
            if (!string.IsNullOrWhiteSpace(certPassword)) options.ServerCertificatePassword = certPassword;
            if (!string.IsNullOrWhiteSpace(upstreamHost)) options.UpstreamHost = upstreamHost;
            if (upstreamPort is > 0) options.UpstreamPort = upstreamPort.Value;
            options.UpstreamTls = upstreamTls || (!upstreamTls && !upstreamNoTls); // default true unless explicitly disabled
            if (!string.IsNullOrWhiteSpace(secret)) options.SharedSecret = secret;
            if (maxSessions is > 0) options.MaxSessions = maxSessions.Value;
            if (sessionRate is >= 0) options.SessionRatePerSecond = sessionRate.Value;

            if (!options.TryValidate(out var error))
            {
                await Console.Error.WriteLineAsync(error ?? "Invalid options").ConfigureAwait(false);
                Environment.ExitCode = 2;
                return;
            }

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(logLevel)
                    .AddSimpleConsole(o =>
                    {
                        o.IncludeScopes = false;
                        o.SingleLine = true;
                        o.ColorBehavior = LoggerColorBehavior.Enabled;
                        o.TimestampFormat = "[HH:mm:ss] ";
                        o.UseUtcTimestamp = false;
                    });
            });
            var logger = loggerFactory.CreateLogger("IrcBouncer.Serve");

            logger.LogInformation("Bouncer server scaffolding configured: bind {Bind}:{Port} (TLS={DTls}), upstream {UHost}:{UPort} (TLS={UTls}), maxSessions={Max}",
                options.BindAddress, options.BindPort, options.DownstreamTls, options.UpstreamHost, options.UpstreamPort, options.UpstreamTls, options.MaxSessions);

            await Console.Out.WriteLineAsync("[INFO] Serve scaffolding in place. Implementation of network listener will follow.").ConfigureAwait(false);
        });

        var root = new RootCommand("Simple IRC bouncer client. Type raw IRC lines to send. Use /quit to exit.")
        {
            serverOption,
            portOption,
            tlsOption,
            noTlsOption,
            nickOption,
            userOption,
            realOption,
            passOption,
            logLevelOption,
            serve
        };

        root.SetAction(async (result, cancellationToken) =>
        {
            var tls = result.GetValue(tlsOption);
            var notls = result.GetValue(noTlsOption);

            if (tls && notls)
            {
                await Console.Error.WriteLineAsync("Options --tls and --notls are mutually exclusive.").ConfigureAwait(false);
                Environment.ExitCode = 2;
                return;
            }

            var server = result.GetValue(serverOption);
            var port = result.GetValue(portOption);
            var nick = result.GetValue(nickOption);
            var user = result.GetValue(userOption);
            var real = result.GetValue(realOption);
            var pass = result.GetValue(passOption);
            var logLevel = result.GetValue(logLevelOption);

            // Configuration precedence: CLI args > Environment variables > Defaults
            var finalServer = string.IsNullOrWhiteSpace(server)
                ? Environment.GetEnvironmentVariable("IRC_SERVER") ?? "irc.libera.chat"
                : server;

            var finalPort = port is > 0
                ? port.Value
                : int.TryParse(Environment.GetEnvironmentVariable("IRC_PORT"), out var envPort) && envPort > 0
                    ? envPort
                    : 6697;

            var finalNick = string.IsNullOrWhiteSpace(nick)
                ? Environment.GetEnvironmentVariable("IRC_NICK") ?? "IrcBouncer" + RandomNumberGenerator.GetInt32(1000, 9999)
                : nick;

            var finalUser = string.IsNullOrWhiteSpace(user)
                ? Environment.GetEnvironmentVariable("IRC_USER") ?? "iruser"
                : user;

            var finalReal = string.IsNullOrWhiteSpace(real)
                ? Environment.GetEnvironmentVariable("IRC_REAL") ?? "Irc Bouncer"
                : real;

            // For password, prefer CLI arg, then environment variable (no default)
            var finalPass = !string.IsNullOrWhiteSpace(pass)
                ? pass
                : Environment.GetEnvironmentVariable("IRC_PASS");

            // TLS configuration: CLI flags take precedence, then environment variable, then default to true
            var useTls = tls || (!tls && !notls && Environment.GetEnvironmentVariable("IRC_TLS")?.ToUpperInvariant() != "FALSE"); // default to true unless explicitly disabled

            // Log level configuration: CLI arg > Environment variable > Default to Information
            var finalLogLevel = logLevel ??
                (Enum.TryParse<LogLevel>(Environment.GetEnvironmentVariable("IRC_LOG_LEVEL"), true, out var envLogLevel)
                    ? envLogLevel
                    : LogLevel.Information);

            // Set up logging
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(finalLogLevel)
                    .AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = false;
                        options.SingleLine = true;
                        options.ColorBehavior = LoggerColorBehavior.Enabled;
                        options.TimestampFormat = "[HH:mm:ss] ";
                        options.UseUtcTimestamp = false;
                    });
            });
            var logger = loggerFactory.CreateLogger("IrcBouncer.Program");

            var code = await RunAsync(finalServer, finalPort, useTls, finalNick, finalUser, finalReal, finalPass, logger, cancellationToken).ConfigureAwait(false);
            Environment.ExitCode = code;
        });

        var parseResult = root.Parse(args);

        foreach (var parseError in parseResult.Errors)
        {
            await Console.Error.WriteLineAsync(parseError.Message).ConfigureAwait(false);
            return 1;
        }

        return await parseResult.InvokeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Parses slash commands and maps them to IRC commands.
    /// Maps "/leave" to "PART" and "/exit" to "QUIT", preserves other commands.
    /// </summary>
    /// <param name="input">The input string to parse</param>
    /// <returns>The parsed IRC command</returns>
    public static string ParseSlashCommand(string input)
    {
        if (!input.StartsWith('/'))
            return input;

        if (input.Length <= 1)
            return input;

        var cmdLine = input[1..];
        var spaceIndex = cmdLine.IndexOf(' ', StringComparison.InvariantCulture);

        if (spaceIndex > 0)
        {
            var commandUpper = cmdLine[..spaceIndex].ToUpperInvariant();
            commandUpper = commandUpper switch
            {
                "LEAVE" => "PART",
                "EXIT" => "QUIT",
                _ => commandUpper
            };
            return commandUpper + cmdLine[spaceIndex..];
        }
        else
        {
            var commandUpper = cmdLine.ToUpperInvariant() switch
            {
                "LEAVE" => "PART",
                "EXIT" => "QUIT",
                var c => c
            };
            return commandUpper;
        }
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
    private static async Task<int> RunAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass, ILogger logger, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var metrics = new IrcMetrics();
        var disconnectedTcs = new TaskCompletionSource<bool>();
        var gracefulShutdownRequested = false;
        var connectionStartTime = DateTime.UtcNow;

        Console.CancelKeyPress += async (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Graceful shutdown requested");
            gracefulShutdownRequested = true;
            await cts.CancelAsync().ConfigureAwait(false);
        };

        logger.LogInformation("Connecting to {Server}:{Port} (TLS={UseTls}) as {Nick} ... Press Ctrl+C to quit",
            server, port, useTls, nick);

        using var connection = new EventTcpClient();
        using var ircClient = new IrcClient(connection);

        ircClient.Connected += (_, _) =>
        {
            logger.LogInformation("Connected and authenticated to IRC server");
            metrics.IncrementConnections(server, useTls);
        };

        ircClient.MessageReceived += (_, message) =>
        {
            logger.LogIncomingMessage(message);
            var command = IrcMetrics.ExtractCommand(message);
            metrics.IncrementMessagesReceived(command);
        };

        ircClient.Error += (_, ex) =>
        {
            logger.LogError(ex, "IRC client error occurred");
            metrics.IncrementErrors("irc_client");
        };

        ircClient.Disconnected += (_, _) =>
        {
            logger.LogInformation("Disconnected from IRC server");
            var connectionDuration = (DateTime.UtcNow - connectionStartTime).TotalSeconds;
            metrics.RecordConnectionDuration(connectionDuration, server);
            metrics.IncrementDisconnections(gracefulShutdownRequested ? "user" : "remote");
            disconnectedTcs.TrySetResult(true);
        };

        // Create a dedicated background task for user input handling, bound to the application lifecycle
        var writeTask = new Task(async void () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var input = Console.ReadLine();
                    if (input == null)
                        break;

                    var toSend = ParseSlashCommand(input);
                    var isCommand = input.StartsWith('/');
                    string? commandUpper = null;

                    if (isCommand && input.Length > 1)
                    {
                        var cmdLine = input[1..];
                        var spaceIndex = cmdLine.IndexOf(' ', StringComparison.InvariantCulture);
                        commandUpper = spaceIndex > 0
                            ? cmdLine[..spaceIndex].ToUpperInvariant()
                            : cmdLine.ToUpperInvariant();
                        commandUpper = commandUpper switch
                        {
                            "LEAVE" => "PART",
                            "EXIT" => "QUIT",
                            _ => commandUpper
                        };
                    }

                    logger.LogOutgoingMessage(toSend);

                    var command = IrcMetrics.ExtractCommand(toSend);
                    var hasSensitiveData = LoggingExtensions.ContainsSensitiveData(toSend);
                    metrics.IncrementMessagesSent(command, hasSensitiveData);

                    await ircClient.SendAsync(toSend, cts.Token);

                    if (isCommand && commandUpper == "QUIT")
                    {
                        ircClient.Disconnect();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Write task error occurred");
                    metrics.IncrementErrors("write_task");
                }
            }
            finally
            {
                await cts.CancelAsync();
            }
        }, cts.Token, TaskCreationOptions.LongRunning);

        // Start the dedicated background task
        writeTask.Start();

        try
        {
            // Start the connection task
            await ircClient.ConnectAsync(server, port, useTls, nick, user, real, pass, cts.Token);

            // Wait for either write task to complete
            await writeTask.ConfigureAwait(false);

            // If graceful shutdown was requested via Ctrl+C, initiate proper shutdown sequence
            if (gracefulShutdownRequested)
            {
                try
                {
                    logger.LogInformation("Sending QUIT command");
                    await ircClient.SendAsync("QUIT :Graceful shutdown", cts.Token).ConfigureAwait(false);

                    logger.LogInformation("Disconnecting from server");
                    ircClient.Disconnect();

                    logger.LogInformation("Waiting for disconnect confirmation");
                    // Wait for Disconnected event with a timeout
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await disconnectedTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("Graceful shutdown timeout, forcing exit");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during graceful shutdown");
                    metrics.IncrementErrors("graceful_shutdown");
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Connection error occurred");
                metrics.IncrementErrors("connection");
            }
        }

        return 0;
    }
}
