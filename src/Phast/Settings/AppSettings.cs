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

    // Instant dark mode for web pages (Ctrl+Shift+D)
    /// <summary>Sites (registrable domains) where dark mode is forced.</summary>
    public List<string> DarkSites { get; set; } = [];
    /// <summary>Force dark mode on every site...</summary>
    public bool DarkAllSites { get; set; }
    /// <summary>...except these (when <see cref="DarkAllSites"/> is on).</summary>
    public List<string> DarkExcludedSites { get; set; } = [];

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
    /// <summary>Built-in Twitch ad blocker (replaces extensions like TwiBlocker).</summary>
    public bool TwitchAdblock { get; set; } = true;
    /// <summary>Built-in YouTube ad blocker.</summary>
    public bool YouTubeAdblock { get; set; } = true;
    /// <summary>Keep the Twitch blocker script up to date from its project (Twitch changes often).</summary>
    public bool SiteScriptsAutoUpdate { get; set; } = true;

    // Startup
    public StartupMode Startup { get; set; } = StartupMode.RestoreSession;
}
