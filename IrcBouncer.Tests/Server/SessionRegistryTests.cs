using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IrcBouncer.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests.Server;

[TestClass]
public class SessionRegistryTests
{
    private sealed class FakeSession : IDownstreamSession
    {
        public string Id { get; }
        public event EventHandler<string>? LineReceived { add { } remove { } }
        public event EventHandler? Disconnected { add { } remove { } }
        public FakeSession(string id) { Id = id; }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(string line, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    [TestMethod]
    public void TryAdd_And_TryGet_Work()
    {
        var reg = new SessionRegistry();
        var s1 = new FakeSession("a");
        var added = reg.TryAdd(s1);
        Assert.IsTrue(added);
        var ok = reg.TryGet("a", out var found);
        Assert.IsTrue(ok);
        Assert.AreSame(s1, found);
    }

    [TestMethod]
    public void TryAdd_Duplicate_ReturnsFalse()
    {
        var reg = new SessionRegistry();
        var s1 = new FakeSession("dup");
        Assert.IsTrue(reg.TryAdd(s1));
        var again = reg.TryAdd(new FakeSession("dup"));
        Assert.IsFalse(again);
    }

    [TestMethod]
    public void Remove_Removes_And_TryGet_Fails()
    {
        var reg = new SessionRegistry();
        var s1 = new FakeSession("x");
        reg.TryAdd(s1);
        var removed = reg.Remove("x");
        Assert.IsTrue(removed);
        var ok = reg.TryGet("x", out var _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void Remove_NonExistent_ReturnsFalse()
    {
        var reg = new SessionRegistry();
        var removed = reg.Remove("nope");
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void Snapshot_Returns_Current_Sessions()
    {
        var reg = new SessionRegistry();
        var s1 = new FakeSession("1");
        var s2 = new FakeSession("2");
        reg.TryAdd(s1);
        reg.TryAdd(s2);
        var snap = reg.Snapshot();
        CollectionAssert.AreEquivalent(new List<IDownstreamSession> { s1, s2 }, new List<IDownstreamSession>(snap));
    }

    [TestMethod]
    public void TryAdd_Null_Throws()
    {
        var reg = new SessionRegistry();
        Assert.ThrowsException<ArgumentNullException>(() => reg.TryAdd(null!));
    }

    [TestMethod]
    public void Remove_NullId_Throws_ArgumentNull()
    {
        var reg = new SessionRegistry();
        Assert.ThrowsException<ArgumentNullException>(() => reg.Remove(null!));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void Remove_EmptyOrWhitespace_Throws_ArgumentException(string id)
    {
        var reg = new SessionRegistry();
        Assert.ThrowsException<ArgumentException>(() => reg.Remove(id));
    }

    [TestMethod]
    public void TryGet_NullId_Throws_ArgumentNull()
    {
        var reg = new SessionRegistry();
        Assert.ThrowsException<ArgumentNullException>(() => reg.TryGet(null!, out _));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    public void TryGet_EmptyOrWhitespace_Throws_ArgumentException(string id)
    {
        var reg = new SessionRegistry();
        Assert.ThrowsException<ArgumentException>(() => reg.TryGet(id, out _));
    }
}
