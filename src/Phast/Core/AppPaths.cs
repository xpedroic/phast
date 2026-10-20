using System.IO;

namespace Phast.Core;

/// <summary>
/// Central place for every file/folder location Phast uses.
/// Everything lives under %LOCALAPPDATA%\Phast so the app stays portable and easy to reset.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Phast");

    /// <summary>WebView2 profile data (cookies, cache, local storage...).</summary>
    public static string UserData => Path.Combine(Root, "User Data");

    /// <summary>Extracted internal pages served as https://phast.internal/...</summary>
    public static string Pages => Path.Combine(Root, "Pages");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string SessionFile => Path.Combine(Root, "session.json");

    public static void EnsureCreated() => Directory.CreateDirectory(Root);
}
