using System.Text.Json.Nodes;
using System.Windows.Input;
using Phast.Input;
using Phast.Tabs;

namespace Phast.Developer;

/// <summary>
/// "Press the new shortcut" mode of the shortcut editor. Key combinations with Ctrl/Alt/F-keys never reach the page
/// (the window handles them first), so the window catches the next one here and sends it back to the page.
/// </summary>
public static class ShortcutCapture
{
    private static BrowserTab? _page;
    private static string? _command;

    public static bool Active => _page != null;

    public static void Start(BrowserTab page, string command)
    {
        _page = page;
        _command = command;
    }

    /// <summary>Called by the window for every key press. Returns true if the key was consumed.</summary>
    public static bool TryCapture(Key key, ModifierKeys modifiers)
    {
        if (_page == null) return false;
        // Wait for a real key, not just a modifier.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.System)
            return true;

        var cancelled = key == Key.Escape && modifiers == ModifierKeys.None;
        var shortcut = cancelled ? null : new Shortcut(key, modifiers).ToString();
        _page.PostMessage(new JsonObject { ["type"] = "shortcutCaptured", ["command"] = _command, ["shortcut"] = shortcut }.ToJsonString());
        _page = null;
        _command = null;
        return true;
    }
}
