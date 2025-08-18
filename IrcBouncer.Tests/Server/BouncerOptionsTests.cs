using IrcBouncer.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests.Server;

[TestClass]
public class BouncerOptionsTests
{
    [TestMethod]
    public void TryValidate_Defaults_Are_Valid()
    {
        var opt = new BouncerOptions();
        var ok = opt.TryValidate(out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryValidate_Invalid_BindAddress()
    {
        var opt = new BouncerOptions { BindAddress = "  " };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "BindAddress cannot be empty");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(70000)]
    public void TryValidate_Invalid_BindPort(int port)
    {
        var opt = new BouncerOptions { BindPort = port };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "BindPort must be between 1 and 65535");
    }

    [TestMethod]
    public void TryValidate_Invalid_UpstreamHost()
    {
        var opt = new BouncerOptions { UpstreamHost = "" };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "UpstreamHost cannot be empty");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(66000)]
    public void TryValidate_Invalid_UpstreamPort(int port)
    {
        var opt = new BouncerOptions { UpstreamPort = port };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "UpstreamPort must be between 1 and 65535");
    }

    [TestMethod]
    public void TryValidate_DownstreamTls_Requires_CertPath()
    {
        var opt = new BouncerOptions { DownstreamTls = true, ServerCertificatePath = null };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "ServerCertificatePath is required when DownstreamTls is enabled");
    }

    [TestMethod]
    public void TryValidate_MaxSessions_Must_Be_Positive()
    {
        var opt = new BouncerOptions { MaxSessions = 0 };
        var ok = opt.TryValidate(out var error);
        Assert.IsFalse(ok);
        StringAssert.Contains(error, "MaxSessions must be greater than 0");
    }

    [TestMethod]
    public void TryValidate_All_Good_With_Tls()
    {
        var opt = new BouncerOptions
        {
            DownstreamTls = true,
            ServerCertificatePath = "somefile.pfx", // non-empty is enough for current validation
            BindAddress = "127.0.0.1",
            BindPort = 6667,
            UpstreamHost = "irc.example.com",
            UpstreamPort = 6697,
            MaxSessions = 10
        };
        var ok = opt.TryValidate(out var error);
        Assert.IsTrue(ok, error);
        Assert.IsNull(error);
    }
}
