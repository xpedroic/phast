namespace Phast.Settings;

public enum ThemeMode { System, Light, Dark }

public enum StartupMode { RestoreSession, NewTab, Pages }

/// <summary>A saved group of pages ("Trabajo", "Ocio"...) opened together from the toolbar.</summary>
public sealed class PageGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Color { get; set; } = "blue";
    public List<string> Urls { get; set; } = [];
}

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

    /// <summary>Pages opened at startup when <see cref="Startup"/> is <see cref="StartupMode.Pages"/>.</summary>
    public List<string> StartupPages { get; set; } = [];

    // Downloads
    /// <summary>Download folder ("" = the Windows Downloads folder).</summary>
    public string DownloadFolder { get; set; } = "";
    /// <summary>Ask where to save every download.</summary>
    public bool AskWhereToSave { get; set; }

    // Page groups
    public List<PageGroup> PageGroups { get; set; } = [];

    // ----- Advanced / developer mode -----
    public bool DeveloperMode { get; set; }

    // Performance
    /// <summary>Background tabs are suspended (frozen, memory trimmed) after this many minutes. 0 = never.</summary>
    public int SleepAfterMinutes { get; set; } = 5;
    /// <summary>Background tabs are fully unloaded (keeping URL and title) after this many minutes. 0 = never.</summary>
    public int DiscardAfterMinutes { get; set; } = 60;
    /// <summary>Maximum loaded tabs; the least recently used background tabs are unloaded beyond it. 0 = no limit.</summary>
    public int MaxLoadedTabs { get; set; }
    /// <summary>GPU acceleration (needs a restart).</summary>
    public bool GpuAcceleration { get; set; } = true;
    /// <summary>Extra Chromium/WebView2 command-line flags (needs a restart).</summary>
    public string BrowserFlags { get; set; } = "";

    /// <summary>Keyboard shortcut overrides: command id → shortcut text ("Ctrl+Shift+K"); empty text = no shortcut.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = [];

    // Ad blocking (advanced)
    /// <summary>Extra filter list URLs added by the user.</summary>
    public List<string> CustomFilterLists { get; set; } = [];
    /// <summary>User's own filter rules (Adblock Plus syntax, one per line).</summary>
    public string CustomFilterRules { get; set; } = "";

    /// <summary>Per-site tweaks, keyed by site (registrable domain, e.g. "example.com").</summary>
    public Dictionary<string, SiteOverride> SiteOverrides { get; set; } = [];
}

/// <summary>Developer tweaks for one site.</summary>
public sealed class SiteOverride
{
    public bool JavaScriptDisabled { get; set; }
    /// <summary>Custom user agent ("" = default).</summary>
    public string UserAgent { get; set; } = "";
    /// <summary>CSS injected into every page of the site.</summary>
    public string Css { get; set; } = "";
    /// <summary>JavaScript run on every page of the site (when the page has loaded).</summary>
    public string Js { get; set; } = "";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => !JavaScriptDisabled && UserAgent.Length == 0 && Css.Length == 0 && Js.Length == 0;
}
