using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Phast.Adblock;
using Phast.Settings;
using Phast.Tabs;

namespace Phast.Controls;

/// <summary>Popup of the shield button: blocked count for the page and an on/off switch for the site.</summary>
public partial class AdblockBubble : UserControl
{
    private Popup? _popup;
    private BrowserTab? _tab;
    private bool _refreshing; // ignore switch changes made by Refresh() itself

    public AdblockBubble() => InitializeComponent();

    /// <summary>Opens Settings at the ad blocking section.</summary>
    public event Action? SettingsRequested;

    public void Show(Popup popup, FrameworkElement anchor, BrowserTab tab)
    {
        _popup = popup;
        _tab = tab;
        Refresh();
        popup.PlacementTarget = anchor;
        popup.Placement = PlacementMode.Bottom;
        popup.HorizontalOffset = -(Width - anchor.ActualWidth) + 8;
        popup.IsOpen = true;
    }

    public void Refresh()
    {
        if (_tab == null) return;
        var host = _tab.DocumentHost;
        var isWeb = host.Length > 0 && !Core.InternalPages.IsInternal(_tab.Url);
        HostText.Text = isWeb ? host : "Página interna de Phast";

        var globallyOn = SettingsService.Current.AdblockEnabled;
        var siteOn = isWeb && AdblockService.IsActiveFor(host);
        SiteRow.Visibility = isWeb && globallyOn ? Visibility.Visible : Visibility.Collapsed;
        _refreshing = true;
        SiteToggle.IsChecked = siteOn;
        _refreshing = false;

        CountText.Text = _tab.BlockedCount.ToString("N0");
        CountLabel.Text = _tab.BlockedCount == 1 ? "elemento bloqueado en esta página" : "elementos bloqueados en esta página";

        string? status = null;
        if (!globallyOn) status = "El bloqueador está desactivado. Puedes activarlo en los ajustes.";
        else if (AdblockService.Engine == null) status = "Descargando las listas de filtros...";
        else if (isWeb && !siteOn) status = "El bloqueo está desactivado en este sitio.";
        StatusText.Text = status ?? "";
        StatusText.Visibility = status != null ? Visibility.Visible : Visibility.Collapsed;
    }

    // Checked/Unchecked (not Click) so mouse, keyboard and accessibility tools all work.
    private void SiteToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_refreshing || _tab == null || _tab.DocumentHost.Length == 0) return;
        AdblockService.SetAllowed(_tab.DocumentHost, allowed: SiteToggle.IsChecked != true);
        _tab.Core?.Reload();
        Refresh();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (_popup != null) _popup.IsOpen = false;
        SettingsRequested?.Invoke();
    }
}
