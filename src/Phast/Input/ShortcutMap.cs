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
    public const string DarkMode = "darkMode";
    /// <summary>"tab1".."tab8": jump to the n-th tab.</summary>
    public static string GoToTab(int n) => "tab" + n;

    /// <summary>Every configurable command with its name in the Settings shortcut editor (in display order).</summary>
    public static readonly IReadOnlyList<(string Id, string Name)> All =
    [
        (NewTab, "Nueva pestaña"), (NewWindow, "Nueva ventana"), (CloseTab, "Cerrar pestaña"), (ReopenTab, "Reabrir pestaña cerrada"),
        (NextTab, "Pestaña siguiente"), (PreviousTab, "Pestaña anterior"),
        (GoToTab(1), "Ir a la pestaña 1"), (GoToTab(2), "Ir a la pestaña 2"), (GoToTab(3), "Ir a la pestaña 3"), (GoToTab(4), "Ir a la pestaña 4"),
        (GoToTab(5), "Ir a la pestaña 5"), (GoToTab(6), "Ir a la pestaña 6"), (GoToTab(7), "Ir a la pestaña 7"), (GoToTab(8), "Ir a la pestaña 8"),
        (LastTab, "Ir a la última pestaña"), (FocusAddressBar, "Ir a la barra de direcciones"), (Reload, "Recargar"),
        (HardReload, "Recargar sin caché"), (Back, "Atrás"), (Forward, "Adelante"), (Home, "Página de inicio"),
        (Bookmark, "Añadir a marcadores"), (ToggleBookmarksBar, "Mostrar u ocultar la barra de marcadores"), (BookmarkManager, "Gestor de marcadores"),
        (DarkMode, "Modo oscuro en esta web"), (FullScreen, "Pantalla completa"), (Print, "Imprimir"), (DevTools, "Herramientas para desarrolladores"),
        (Settings, "Configuración"),
    ];
}

/// <summary>A key + modifiers combination, e.g. Ctrl+Shift+T. Text form: "Ctrl+Alt+Shift+Key" (Key = WPF key name).</summary>
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
            Key.PageDown => "PageDown",   // same value as Key.Next
            Key.PageUp => "PageUp",       // same value as Key.Prior
            Key.Enter => "Enter",         // same value as Key.Return
            _ => Key.ToString(),
        });
        return string.Join("+", parts);
    }

    /// <summary>Parses the text form ("Ctrl+Shift+T", "Alt+Left", "F5", "Ctrl+1"). Returns null if invalid.</summary>
    public static Shortcut? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var mods = ModifierKeys.None;
        Key? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= ModifierKeys.Control; continue;
                case "alt": mods |= ModifierKeys.Alt; continue;
                case "shift" or "mayús" or "mayus": mods |= ModifierKeys.Shift; continue;
            }
            if (raw.Length == 1 && char.IsAsciiDigit(raw[0])) key = Key.D0 + (raw[0] - '0');
            else if (Enum.TryParse<Key>(raw, ignoreCase: true, out var k)) key = k;
            else return null;
        }
        return key is { } kk && kk != Key.None ? new Shortcut(kk, mods) : null;
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
        m.Bind(Commands.DarkMode, Key.D, C | S);
        return m;
    }

    public void Bind(string command, Key key, ModifierKeys modifiers) => _map[new Shortcut(key, modifiers)] = command;

    /// <summary>Defaults with the user's overrides applied (an override replaces every default shortcut of its command).</summary>
    public static ShortcutMap Create(IReadOnlyDictionary<string, string> overrides)
    {
        var m = CreateDefault();
        foreach (var (command, text) in overrides)
        {
            foreach (var key in m._map.Where(kv => kv.Value == command).Select(kv => kv.Key).ToList()) m._map.Remove(key);
            if (Shortcut.Parse(text) is { } s) m._map[s] = command; // also takes it away from any other command
        }
        return m;
    }

    /// <summary>All shortcuts of a command, as text.</summary>
    public IEnumerable<string> ShortcutsOf(string command) =>
        _map.Where(kv => kv.Value == command).Select(kv => kv.Key.ToString());

    public string? Find(Key key, ModifierKeys modifiers) =>
        _map.TryGetValue(new Shortcut(key, modifiers), out var cmd) ? cmd : null;

    /// <summary>First shortcut bound to a command, for showing in menus.</summary>
    public string GestureText(string command) =>
        _map.FirstOrDefault(kv => kv.Value == command).Key is { Key: not Key.None } s ? s.ToString() : "";
}
