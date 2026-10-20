using System.Windows.Input;
using Phast.Input;
using Xunit;

namespace Phast.Tests;

public class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+Shift+T", Key.T, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData("Alt+Left", Key.Left, ModifierKeys.Alt)]
    [InlineData("F5", Key.F5, ModifierKeys.None)]
    [InlineData("Ctrl+1", Key.D1, ModifierKeys.Control)]
    [InlineData("ctrl + k", Key.K, ModifierKeys.Control)]
    public void Parse(string text, Key key, ModifierKeys mods) =>
        Assert.Equal(new Shortcut(key, mods), Shortcut.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Banana")]
    public void Parse_Invalid(string text) => Assert.Null(Shortcut.Parse(text));

    [Fact]
    public void RoundTrip() =>
        Assert.Equal("Ctrl+Shift+7", new Shortcut(Key.D7, ModifierKeys.Control | ModifierKeys.Shift).ToString());

    [Fact]
    public void Override_ReplacesDefaults_AndStealsFromOtherCommands()
    {
        var map = ShortcutMap.Create(new Dictionary<string, string>
        {
            [Commands.NewTab] = "Ctrl+Shift+N",     // was Ctrl+T
            [Commands.Print] = "",                  // no shortcut at all
            [Commands.DarkMode] = "Ctrl+R",         // taken from Reload
        });
        Assert.Equal(Commands.NewTab, map.Find(Key.N, ModifierKeys.Control | ModifierKeys.Shift));
        Assert.Null(map.Find(Key.T, ModifierKeys.Control));
        Assert.Null(map.Find(Key.P, ModifierKeys.Control));
        Assert.Equal(Commands.DarkMode, map.Find(Key.R, ModifierKeys.Control));
        Assert.Equal(Commands.Reload, map.Find(Key.F5, ModifierKeys.None)); // Reload keeps its other default
    }
}
