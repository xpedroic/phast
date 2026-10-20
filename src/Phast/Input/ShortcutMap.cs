using System.Windows.Input;

namespace Phast.Input;

/// <summary>Identifiers of every browser action that can be bound to a keyboard shortcut.</summary>
public static class Commands
{
    public const string NewTab = "newTab";
    public const string NewWindow = "newWindow";
    public const string CloseTab = "closeTab";
    public const string ReopenTab = "reopenTab";
    public const string NextTab = "nextTab";
    public const string PreviousTab = "previousTab";
    public const string LastTab = "lastTab";
    public const string FocusAddressBar = "focusAddressBar";
    public const string Reload = "reload";
    public const string HardReload = "hardReload";
    public const string Back = "back";
    public const string Forward = "forward";
    public const string DevTools = "devTools";
    public const string FullScreen = "fullScreen";
    public const string Print = "print";
    public const string Home = "home";
    public const string Settings = "settings";
    public const string Bookmark = "bookmark";
    public const string ToggleBookmarksBar = "toggleBookmarksBar";
    public const string BookmarkManager = "bookmarkManager";
    /// <summary>"tab1".."tab8": jump to the n-th tab.</summary>
    public static string GoToTab(int n) => "tab" + n;
}

/// <summary>A key + modifiers combination, e.g. Ctrl+Shift+T.</summary>
public readonly record struct Shortcut(Key Key, ModifierKeys Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(Key - Key.D0)).ToString(),
            Key.Left => "←",
            Key.Right => "→",
            _ => Key.ToString(),
        });
        return string.Join("+", parts);
    }
}

/// <summary>
/// Maps shortcuts to commands. Holds Chrome's default bindings; several shortcuts may trigger the same command.
/// (User customization will be stored on top of these defaults from Settings > Advanced.)
/// </summary>
public sealed class ShortcutMap
{
    private readonly Dictionary<Shortcut, string> _map = new();

    public static ShortcutMap CreateDefault()
    {
        var m = new ShortcutMap();
        const ModifierKeys C = ModifierKeys.Control, S = ModifierKeys.Shift, A = ModifierKeys.Alt;
        m.Bind(Commands.NewTab, Key.T, C);
        m.Bind(Commands.NewWindow, Key.N, C);
        m.Bind(Commands.CloseTab, Key.W, C);
        m.Bind(Commands.CloseTab, Key.F4, C);
        m.Bind(Commands.ReopenTab, Key.T, C | S);
        m.Bind(Commands.NextTab, Key.Tab, C);
        m.Bind(Commands.NextTab, Key.PageDown, C);
        m.Bind(Commands.PreviousTab, Key.Tab, C | S);
        m.Bind(Commands.PreviousTab, Key.PageUp, C);
        for (int i = 1; i <= 8; i++)
        {
            m.Bind(Commands.GoToTab(i), Key.D0 + i, C);
            m.Bind(Commands.GoToTab(i), Key.NumPad0 + i, C);
        }
        m.Bind(Commands.LastTab, Key.D9, C);
        m.Bind(Commands.LastTab, Key.NumPad9, C);
        m.Bind(Commands.FocusAddressBar, Key.L, C);
        m.Bind(Commands.FocusAddressBar, Key.D, A);
        m.Bind(Commands.FocusAddressBar, Key.F6, ModifierKeys.None);
        m.Bind(Commands.Reload, Key.F5, ModifierKeys.None);
        m.Bind(Commands.Reload, Key.R, C);
        m.Bind(Commands.HardReload, Key.F5, C);
        m.Bind(Commands.HardReload, Key.R, C | S);
        m.Bind(Commands.Back, Key.Left, A);
        m.Bind(Commands.Forward, Key.Right, A);
        m.Bind(Commands.DevTools, Key.F12, ModifierKeys.None);
        m.Bind(Commands.DevTools, Key.I, C | S);
        m.Bind(Commands.FullScreen, Key.F11, ModifierKeys.None);
        m.Bind(Commands.Print, Key.P, C);
        m.Bind(Commands.Home, Key.Home, A);
        m.Bind(Commands.Bookmark, Key.D, C);
        m.Bind(Commands.ToggleBookmarksBar, Key.B, C | S);
        m.Bind(Commands.BookmarkManager, Key.O, C | S);
        return m;
    }

    public void Bind(string command, Key key, ModifierKeys modifiers) => _map[new Shortcut(key, modifiers)] = command;

    public string? Find(Key key, ModifierKeys modifiers) =>
        _map.TryGetValue(new Shortcut(key, modifiers), out var cmd) ? cmd : null;

    /// <summary>First shortcut bound to a command, for showing in menus.</summary>
    public string GestureText(string command) =>
        _map.FirstOrDefault(kv => kv.Value == command).Key is { Key: not Key.None } s ? s.ToString() : "";
}
