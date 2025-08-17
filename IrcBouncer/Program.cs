using System.CommandLine;

namespace IrcBouncer;

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
                await Console.Error.WriteLineAsync("Options --tls and --notls are mutually exclusive.");
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
            var finalNick = string.IsNullOrWhiteSpace(nick) ? ("IrcBouncer" + Random.Shared.Next(1000, 9999)) : nick;
            var finalUser = string.IsNullOrWhiteSpace(user) ? "ircuser" : user;
            var finalReal = string.IsNullOrWhiteSpace(real) ? "Irc Bouncer" : real;

            var useTls = tls || (!tls && !notls); // default to true unless --notls is specified
            var code = await RunAsync(finalServer, finalPort, useTls, finalNick, finalUser, finalReal, pass, cancellationToken);
            Environment.ExitCode = code;
        });
        
        var parseResult = root.Parse(args);
        
        foreach (var parseError in parseResult.Errors)
        {
            await Console.Error.WriteLineAsync(parseError.Message);
            return 1;
        }

        return await parseResult.InvokeAsync();
    }

    private static async Task<int> RunAsync(string server, int port, bool useTls, string nick, string user, string real, string? pass, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        Console.WriteLine($"Connecting to {server}:{port} (TLS={useTls}) as {nick} ... Press Ctrl+C to quit.");

        var client = new EventTcpClient();

        client.Connected += async (_, _) =>
        {
            if (!string.IsNullOrEmpty(pass))
            {
                await client.Write($"$PASS {pass}");
            }
            await client.Write($"NICK {nick}");
            await client.Write($"USER {user} 0 * :{real}");
        };

        client.Data += async (_, message) =>
        {
            Console.WriteLine($"< {message}");
                    
            if (message.StartsWith("PING ", StringComparison.OrdinalIgnoreCase))
            {
                var payload = message[5..];
                await client.Write($"PONG {payload}");
            }
        };

        client.Error += (_, ex) =>
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        };

        client.Disconnected += (_, _) =>
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
                    if (input == null) break;
                    
                    var isCommand = input.StartsWith('/');
                    if (isCommand)
                    {
                        input = input[1..];
                        var spaceIndex = input.IndexOf(' ');
                        if (spaceIndex > 0)
                        {
                            var command = input[..spaceIndex].ToUpperInvariant();
                            command = command switch
                            {
                                "LEAVE" => "PART",
                                "EXIT" => "QUIT",
                                _ => command
                            };
                            input = command + input[spaceIndex..];
                        }
                        else
                            input = input.ToUpperInvariant();
                    }

                    Console.Out.WriteLine($"> {input}");
                    
                    _ = client.Write(input);
                    
                    if (isCommand)
                    {
                        input = input[1..];
                        var spaceIndex = input.IndexOf(' ');
                        if (spaceIndex > 0)
                            switch (input[..spaceIndex])
                            {
                                case "QUIT":
                                    client.Disconnect();
                                    return;
                            }
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

        await Task.WhenAny(writeTask, client.ConnectAsync(server, port, true, cts.Token));
        
        return 0;
    }
}