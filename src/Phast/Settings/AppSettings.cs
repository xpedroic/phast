namespace Phast.Settings;

public enum ThemeMode { System, Light, Dark }

public enum StartupMode { RestoreSession, NewTab }

/// <summary>
/// All user-configurable options. Serialized to settings.json.
/// New options must have a sensible default so old settings files keep working.
/// </summary>
public sealed class AppSettings
{
    // Appearance
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public bool ShowBookmarksBar { get; set; } = true;

    // Search
    public string SearchEngine { get; set; } = "google";
    /// <summary>Ask the search engine for suggestions while typing in the address bar.</summary>
    public bool SearchSuggestions { get; set; } = true;

    // Ad blocking
    public bool AdblockEnabled { get; set; } = true;
    /// <summary>Also hide leftover ad boxes on known sites (site-specific element hiding).</summary>
    public bool AdblockCosmetic { get; set; } = true;
    /// <summary>Ids of the enabled filter lists (see FilterLists.BuiltIn).</summary>
    public List<string> FilterLists { get; set; } = ["easylist", "easyprivacy", "easylist-es"];
    /// <summary>Sites where blocking is turned off by the user (host names).</summary>
    public List<string> AdblockAllowlist { get; set; } = [];

    // Startup
    public StartupMode Startup { get; set; } = StartupMode.RestoreSession;
}
