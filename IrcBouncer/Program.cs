using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

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
        var noTlsOption = new Option<bool>("--notls" ) { Description = "Do not use TLS to connect" };

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

        var root = new RootCommand("Simple IRC bouncer client. Type raw IRC lines to send. Use /quit to exit.")
        {
            serverOption,
            portOption,
            tlsOption,
            noTlsOption,
            nickOption,
            userOption,
            realOption,
            passOption
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
            
            var code = await RunAsync(finalServer, finalPort, useTls, finalNick, finalUser, finalReal, finalPass, cancellationToken).ConfigureAwait(false);
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
    private static async Task<int> RunAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var disconnectedTcs = new TaskCompletionSource<bool>();
        var gracefulShutdownRequested = false;

        Console.CancelKeyPress += async (_, e) => 
        { 
            e.Cancel = true; 
            Console.WriteLine("\nGraceful shutdown requested...");
            gracefulShutdownRequested = true;
            await cts.CancelAsync().ConfigureAwait(false); 
        };

        Console.WriteLine($"Connecting to {server}:{port} (TLS={useTls}) as {nick} ... Press Ctrl+C to quit.");

        using var connection = new EventTcpClient();
        using var ircClient = new IrcClient(connection);

        ircClient.Connected += (_, _) =>
        {
            Console.WriteLine("Connected and authenticated to IRC server.");
        };

        ircClient.MessageReceived += (_, message) =>
        {
            Console.WriteLine($"< {message}");
        };

        ircClient.Error += (_, ex) =>
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        };

        ircClient.Disconnected += (_, _) =>
        {
            Console.WriteLine("Disconnected.");
            disconnectedTcs.TrySetResult(true);
        };
        
        var writeTask = Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var input = Console.ReadLine();
                    if (input == null)
                        break;

                    var original = input;
                    var toSend = ParseSlashCommand(original);
                    var isCommand = original.StartsWith('/');
                    string? commandUpper = null;
                    
                    if (isCommand && original.Length > 1)
                    {
                        var cmdLine = original[1..];
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

                    Console.Out.WriteLine($"> {toSend}");
                    
                    _ = ircClient.SendAsync(toSend);
                    
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
                    Console.Error.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                cts.Cancel();
            }
        }, cts.Token);

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
                    Console.WriteLine("Sending QUIT command...");
                    await ircClient.SendAsync("QUIT :Graceful shutdown", cts.Token).ConfigureAwait(false);
                    
                    Console.WriteLine("Disconnecting...");
                    ircClient.Disconnect();
                    
                    Console.WriteLine("Waiting for disconnect confirmation...");
                    // Wait for Disconnected event with a timeout
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await disconnectedTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Graceful shutdown timeout, forcing exit.");
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Error during graceful shutdown: {ex.Message}").ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                await Console.Error.WriteLineAsync($"Connection error: {ex.Message}").ConfigureAwait(false);
        }
        
        return 0;
    }
}
