using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcBouncer.Tests;

[TestClass]
public class ProgramTests
{
    [TestMethod]
    public void ParseSlashCommand_Leave_MapsToPartWithParameters()
    {
        // Arrange
        var input = "/leave #channel reason here";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("PART #channel reason here", result, "LEAVE command with parameters should map to PART");
        Console.WriteLine("[DEBUG_LOG] LEAVE command mapping with parameters verified");
    }

    [TestMethod]
    public void ParseSlashCommand_Leave_MapsToPartWithoutParameters()
    {
        // Arrange
        var input = "/leave";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("PART", result);
        Console.WriteLine("[DEBUG_LOG] LEAVE command mapping without parameters verified");
    }

    [TestMethod]
    public void ParseSlashCommand_Exit_MapsToQuitWithParameters()
    {
        // Arrange
        var input = "/exit goodbye message";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("QUIT goodbye message", result);
        Console.WriteLine("[DEBUG_LOG] EXIT command mapping with parameters verified");
    }

    [TestMethod]
    public void ParseSlashCommand_Exit_MapsToQuitWithoutParameters()
    {
        // Arrange
        var input = "/exit";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("QUIT", result);
        Console.WriteLine("[DEBUG_LOG] EXIT command mapping without parameters verified");
    }

    [TestMethod]
    public void ParseSlashCommand_RegularCommand_PassesThrough()
    {
        // Arrange
        var input = "/join #channel";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("JOIN #channel", result);
        Console.WriteLine("[DEBUG_LOG] Regular slash command pass-through verified");
    }

    [TestMethod]
    public void ParseSlashCommand_NonSlashCommand_ReturnsAsIs()
    {
        // Arrange
        var input = "Hello world";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("Hello world", result);
        Console.WriteLine("[DEBUG_LOG] Non-slash command pass-through verified");
    }

    [TestMethod]
    public void ParseSlashCommand_EmptySlashCommand_ReturnsSlash()
    {
        // Arrange
        var input = "/";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("/", result);
        Console.WriteLine("[DEBUG_LOG] Empty slash command handling verified");
    }

    [TestMethod]
    public void ParseSlashCommand_CaseInsensitive_MapsCorrectly()
    {
        // Arrange
        var input = "/LEAVE #channel";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("PART #channel", result);
        Console.WriteLine("[DEBUG_LOG] Case insensitive command mapping verified");
    }

    [TestMethod]
    public void ParseSlashCommand_MixedCase_MapsCorrectly()
    {
        // Arrange
        var input = "/eXiT goodbye";

        // Act
        var result = Program.ParseSlashCommand(input);

        // Assert
        Assert.AreEqual("QUIT goodbye", result);
        Console.WriteLine("[DEBUG_LOG] Mixed case command mapping verified");
    }
}
