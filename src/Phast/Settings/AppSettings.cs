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

    // Startup
    public StartupMode Startup { get; set; } = StartupMode.RestoreSession;
}
