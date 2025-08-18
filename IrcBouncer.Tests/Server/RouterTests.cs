using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IrcBouncer.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests.Server;

[TestClass]
public class RouterTests
{
    private sealed class FakeSession : IDownstreamSession
    {
        public string Id { get; }
        public event EventHandler<string>? LineReceived { add { } remove { } }
        public event EventHandler? Disconnected { add { } remove { } }
        public List<string> Received { get; } = new();
        private readonly bool _throwOnSend;
        public FakeSession(string id, bool throwOnSend = false) { Id = id; _throwOnSend = throwOnSend; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(string line, CancellationToken cancellationToken = default)
        {
            if (_throwOnSend) throw new InvalidOperationException("boom");
            lock (Received) Received.Add(line);
            return Task.CompletedTask;
        }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class FakeUpstream : IUpstreamConnection
    {
        public bool IsConnected => true;
        public event EventHandler<string>? LineReceived { add { } remove { } }
        public event EventHandler? Disconnected { add { } remove { } }
        public readonly List<string> Sent = new();
        private int _concurrent;
        public int MaxConcurrentObserved;
        private readonly int _delayMs;
        public FakeUpstream(int delayMs = 10) { _delayMs = delayMs; }
        public Task ConnectAsync(string host, int port, bool useTls, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public async Task SendAsync(string line, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _concurrent);
            if (now > MaxConcurrentObserved) MaxConcurrentObserved = now;
            try
            {
                await Task.Delay(_delayMs, cancellationToken);
                lock (Sent) Sent.Add(line);
            }
            finally
            {
                Interlocked.Decrement(ref _concurrent);
            }
        }
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    [TestMethod]
    public async Task FanOut_Sends_To_All_Sessions_And_Ignores_Errors()
    {
        var upstream = new FakeUpstream();
        using var router = new Router(upstream);
        var ok = new FakeSession("ok");
        var bad = new FakeSession("bad", throwOnSend: true);
        router.AddSession(ok);
        router.AddSession(bad);

        await router.FanOutAsync("PING :123");

        CollectionAssert.AreEqual(new List<string> { "PING :123" }, ok.Received);
    }

    [TestMethod]
    public async Task FanIn_Serializes_Upstream_Writes()
    {
        var upstream = new FakeUpstream(delayMs: 25);
        using var router = new Router(upstream);

        var tasks = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(router.FanInAsync($"MSG {i}", sessionId: "s1"));
        }
        await Task.WhenAll(tasks);

        Assert.IsTrue(upstream.MaxConcurrentObserved <= 1, $"Observed concurrency {upstream.MaxConcurrentObserved}");
        Assert.AreEqual(5, upstream.Sent.Count);
    }

    [TestMethod]
    public async Task Add_Remove_And_Dispose_Behavior()
    {
        var upstream = new FakeUpstream();
        var router = new Router(upstream);
        var s1 = new FakeSession("1");
        router.AddSession(s1);
        router.RemoveSession("1");
        router.Dispose();
        Assert.ThrowsException<ObjectDisposedException>(() => router.AddSession(s1));
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await router.FanOutAsync("x"));
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(async () => await router.FanInAsync("y", "1"));
    }

    [TestMethod]
    public async Task Argument_Validation()
    {
        var upstream = new FakeUpstream();
        using var router = new Router(upstream);
        Assert.ThrowsException<ArgumentNullException>(() => router.AddSession(null!));
        Assert.ThrowsException<ArgumentException>(() => router.RemoveSession(" "));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await router.FanOutAsync(null!));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await router.FanInAsync(null!, "id"));
        await Assert.ThrowsExceptionAsync<ArgumentException>(async () => await router.FanInAsync("x", " "));
    }
}
