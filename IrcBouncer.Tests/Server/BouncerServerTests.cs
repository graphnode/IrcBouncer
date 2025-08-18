using System;
using IrcBouncer.Server;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests.Server;

[TestClass]
public class BouncerServerTests
{
    private static ILogger CreateLogger() => LoggerFactory.Create(b => { }).CreateLogger("test");

    [TestMethod]
    public void Ctor_Null_Args_Throw()
    {
        var logger = CreateLogger();
        var sessions = new SessionRegistry();
        var options = new BouncerOptions();

        Assert.ThrowsException<ArgumentNullException>(() => new BouncerServer(null!, sessions, logger));
        Assert.ThrowsException<ArgumentNullException>(() => new BouncerServer(options, null!, logger));
        Assert.ThrowsException<ArgumentNullException>(() => new BouncerServer(options, sessions, null!));
    }

    [TestMethod]
    public void Ctor_Invalid_Options_Throws()
    {
        var logger = CreateLogger();
        var sessions = new SessionRegistry();
        var options = new BouncerOptions { MaxSessions = 0 }; // invalid per TryValidate

        var ex = Assert.ThrowsException<ArgumentException>(() => new BouncerServer(options, sessions, logger));
        StringAssert.Contains(ex.Message, "MaxSessions must be greater than 0");
    }

    [TestMethod]
    public void Ctor_Valid_Options_Succeeds()
    {
        var logger = CreateLogger();
        var sessions = new SessionRegistry();
        var options = new BouncerOptions();

        using var server = new BouncerServer(options, sessions, logger);
        Assert.IsNotNull(server);
    }
}
