using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class IrcCommandTests
{
    [TestMethod]
    public void IrcMessage_Parse_ValidMessage_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = ":irc.example.com 001 testnick :Welcome to the IRC Network";

        // Act
        var message = IrcMessage.Parse(rawMessage);

        // Assert
        Assert.AreEqual("irc.example.com", message.Prefix);
        Assert.AreEqual("001", message.Command);
        Assert.AreEqual(1, message.Parameters.Count);
        Assert.AreEqual("testnick", message.Parameters[0]);
        Assert.AreEqual("Welcome to the IRC Network", message.Trailing);
    }

    [TestMethod]
    public void IrcMessage_Parse_MessageWithoutPrefix_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = "PING :irc.example.com";

        // Act
        var message = IrcMessage.Parse(rawMessage);

        // Assert
        Assert.IsNull(message.Prefix);
        Assert.AreEqual("PING", message.Command);
        Assert.AreEqual(0, message.Parameters.Count);
        Assert.AreEqual("irc.example.com", message.Trailing);
    }

    [TestMethod]
    public void IrcMessage_Parse_MessageWithMultipleParameters_ParsesCorrectly()
    {
        // Arrange
        var rawMessage = ":nick!user@host PRIVMSG #channel :Hello world!";

        // Act
        var message = IrcMessage.Parse(rawMessage);

        // Assert
        Assert.AreEqual("nick!user@host", message.Prefix);
        Assert.AreEqual("PRIVMSG", message.Command);
        Assert.AreEqual(1, message.Parameters.Count);
        Assert.AreEqual("#channel", message.Parameters[0]);
        Assert.AreEqual("Hello world!", message.Trailing);
    }

    [TestMethod]
    public void IrcMessage_Format_ValidMessage_FormatsCorrectly()
    {
        // Arrange
        var message = new IrcMessage
        {
            Prefix = "nick!user@host",
            Command = "PRIVMSG",
            Parameters = ["#channel"],
            Trailing = "Hello world!"
        };

        // Act
        var formatted = message.Format();

        // Assert
        Assert.AreEqual(":nick!user@host PRIVMSG #channel :Hello world!", formatted);
    }

    [TestMethod]
    public void IrcMessage_Create_SimpleMessage_CreatesCorrectly()
    {
        // Arrange & Act
        var message = IrcMessage.Create("PONG", "irc.example.com");

        // Assert
        Assert.AreEqual("PONG", message.Command);
        Assert.AreEqual("irc.example.com", message.Trailing);
        Assert.IsNull(message.Prefix);
        Assert.AreEqual(0, message.Parameters.Count);
    }

    [TestMethod]
    public void IrcMessage_Create_MessageWithParameters_CreatesCorrectly()
    {
        // Arrange & Act
        var message = IrcMessage.Create("PRIVMSG", ["#channel"], "Hello world!");

        // Assert
        Assert.AreEqual("PRIVMSG", message.Command);
        Assert.AreEqual(1, message.Parameters.Count);
        Assert.AreEqual("#channel", message.Parameters[0]);
        Assert.AreEqual("Hello world!", message.Trailing);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void IrcMessage_Parse_EmptyMessage_ThrowsException()
    {
        // Act
        IrcMessage.Parse("");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void IrcMessage_Parse_MessageWithoutCommand_ThrowsException()
    {
        // Act
        IrcMessage.Parse(":prefix");
    }

    [TestMethod]
    public void IrcMessage_Format_RoundTrip_PreservesMessage()
    {
        // Arrange
        var original = ":nick!user@host PRIVMSG #channel :Hello world!";

        // Act
        var parsed = IrcMessage.Parse(original);
        var formatted = parsed.Format();

        // Assert
        Assert.AreEqual(original, formatted);
    }
}
