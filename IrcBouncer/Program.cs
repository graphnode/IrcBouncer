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
            
            var finalServer = string.IsNullOrWhiteSpace(server) ? "irc.libera.chat" : server;
            var finalPort = port is > 0 ? port.Value : 6697;
            
            var finalNick = string.IsNullOrWhiteSpace(nick) ? ("IrcBouncer" + RandomNumberGenerator.GetInt32(1000, 9999)) : nick;
            var finalUser = string.IsNullOrWhiteSpace(user) ? "iruser" : user;
            var finalReal = string.IsNullOrWhiteSpace(real) ? "Irc Bouncer" : real;

            var useTls = tls || (!tls && !notls); // default to true unless --notls is specified
            var code = await RunAsync(finalServer, finalPort, useTls, finalNick, finalUser, finalReal, pass, cancellationToken).ConfigureAwait(false);
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance")]
    private static async Task<int> RunAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

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
                    var isCommand = original.StartsWith('/');
                    var toSend = original;
                    string? commandUpper = null;
                    if (isCommand)
                    {
                        var cmdLine = original[1..];
                        var spaceIndex = cmdLine.IndexOf(' ', StringComparison.Ordinal);
                        if (spaceIndex > 0)
                        {
                            commandUpper = cmdLine[..spaceIndex].ToUpperInvariant();
                            commandUpper = commandUpper switch
                            {
                                "LEAVE" => "PART",
                                "EXIT" => "QUIT",
                                _ => commandUpper
                            };
                            toSend = commandUpper + cmdLine[spaceIndex..];
                        }
                        else
                        {
                            commandUpper = cmdLine.ToUpperInvariant() switch
                            {
                                "LEAVE" => "PART",
                                "EXIT" => "QUIT",
                                var c => c
                            };
                            toSend = commandUpper;
                        }
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

        await Task.WhenAny(writeTask, ircClient.ConnectAsync(server, port, useTls, nick, user, real, pass, cts.Token)).ConfigureAwait(false);
        
        return 0;
    }
}
