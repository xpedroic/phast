using System.Windows;
using Phast.Settings;

namespace Phast;

/// <summary>Shield button: shows how many requests were blocked on the current page.</summary>
public partial class MainWindow
{
    private void InitAdblockUi()
    {
        AdblockPanel.SettingsRequested += () => OpenInternalPage("phast://settings#privacy");
        SettingsService.Changed += OnAdblockSettingsChanged;
        Closed += (_, _) => SettingsService.Changed -= OnAdblockSettingsChanged;
    }

    private void OnAdblockSettingsChanged()
    {
        // Turning the blocker on must start routing requests in tabs that are already open.
        foreach (var tab in Tabs.Tabs) tab.EnsureRequestFilter();
        UpdateShield();
    }

    private void UpdateShield()
    {
        var tab = Tabs.Active;
        var count = tab?.BlockedCount ?? 0;
        var active = tab?.BlockingActive == true;
        ShieldBadge.Visibility = active && count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ShieldCount.Text = count > 99 ? "99+" : count.ToString();
        ShieldGlyph.Opacity = active ? 1 : 0.45;
        ShieldButton.ToolTip = !SettingsService.Current.AdblockEnabled ? "Bloqueador de anuncios (desactivado)"
            : active ? $"Bloqueador de anuncios: {count} bloqueados en esta página"
            : "Bloqueador de anuncios (desactivado en este sitio)";
        if (AdblockPopup.IsOpen) AdblockPanel.Refresh();
    }

    private void Shield_Click(object sender, RoutedEventArgs e)
    {
        if (Tabs.Active != null) AdblockPanel.Show(AdblockPopup, ShieldButton, Tabs.Active);
    }
}
