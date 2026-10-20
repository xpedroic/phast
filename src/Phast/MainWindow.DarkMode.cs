using System.Windows;
using Phast.Core;
using Phast.Dark;

namespace Phast;

/// <summary>Moon button / Ctrl+Shift+D: instant dark mode for the current site.</summary>
public partial class MainWindow
{
    private void InitDarkModeUi()
    {
        DarkModeService.Changed += OnDarkModeChanged;
        Closed += (_, _) => DarkModeService.Changed -= OnDarkModeChanged;
    }

    private void OnDarkModeChanged()
    {
        foreach (var tab in Tabs.Tabs) tab.RefreshDarkModeAsync().Forget();
    }

    private void UpdateDarkButton()
    {
        var tab = Tabs.Active;
        var isWeb = IsWebPage(tab);
        DarkButton.IsEnabled = isWeb;
        if (tab?.IsDarkForced == true) DarkButton.SetResourceReference(ForegroundProperty, "AccentBrush");
        else DarkButton.ClearValue(ForegroundProperty);
        DarkButton.ToolTip = tab?.IsDarkForced == true
            ? "Quitar el modo oscuro en esta web (Ctrl+Mayús+D)"
            : "Modo oscuro en esta web (Ctrl+Mayús+D)";
    }

    private void Dark_Click(object sender, RoutedEventArgs e) => ToggleDarkMode();

    /// <summary>Switches dark mode for the current site; every open tab of that site follows instantly.</summary>
    public void ToggleDarkMode()
    {
        var tab = Tabs.Active;
        if (!IsWebPage(tab)) return;
        DarkModeService.Toggle(tab!.DocumentHost); // raises DarkModeService.Changed → all tabs refresh
    }

    /// <summary>A real web page (DocumentHost follows the page being loaded; Url only updates a bit later).</summary>
    private static bool IsWebPage(Tabs.BrowserTab? tab) =>
        tab != null && tab.DocumentHost.Length > 0 && tab.DocumentHost != InternalPages.Host;
}
