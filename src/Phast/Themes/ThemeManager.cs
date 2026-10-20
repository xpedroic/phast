using System.Windows;
using Microsoft.Win32;
using Phast.Settings;

namespace Phast.Themes;

/// <summary>
/// Switches the UI between the light and dark palettes.
/// In "System" mode it follows the Windows app theme and reacts live when it changes.
/// </summary>
public static class ThemeManager
{
    private static readonly Uri LightUri = new("pack://application:,,,/Themes/Light.xaml");
    private static readonly Uri DarkUri = new("pack://application:,,,/Themes/Dark.xaml");
    private static ResourceDictionary? _current;

    public static bool IsDark { get; private set; }

    /// <summary>Color scheme web pages see through prefers-color-scheme.</summary>
    public static Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme PreferredScheme =>
        SettingsService.Current.Theme switch
        {
            ThemeMode.Dark => Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Dark,
            ThemeMode.Light => Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Light,
            _ => Microsoft.Web.WebView2.Core.CoreWebView2PreferredColorScheme.Auto,
        };

    /// <summary>Raised after the palette changed (windows update their native frame, pages their color scheme).</summary>
    public static event Action? ThemeChanged;

    public static void Initialize()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && SettingsService.Current.Theme == ThemeMode.System)
                Application.Current.Dispatcher.BeginInvoke(Apply);
        };
        SettingsService.Changed += Apply;
    }

    public static void Apply()
    {
        var dark = SettingsService.Current.Theme switch
        {
            ThemeMode.Dark => true,
            ThemeMode.Light => false,
            _ => IsWindowsAppThemeDark(),
        };
        if (_current != null && dark == IsDark)
        {
            ThemeChanged?.Invoke(); // palette unchanged, but pages may need a new preferred color scheme
            return;
        }

        IsDark = dark;
        var dict = new ResourceDictionary { Source = dark ? DarkUri : LightUri };
        var merged = Application.Current.Resources.MergedDictionaries;
        if (_current != null) merged.Remove(_current);
        merged.Insert(0, dict);
        _current = dict;
        ThemeChanged?.Invoke();
    }

    private static bool IsWindowsAppThemeDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int v && v == 0;
    }
}
