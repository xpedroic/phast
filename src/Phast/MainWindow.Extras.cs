using System.Windows;
using Phast.Core;

namespace Phast;

/// <summary>"Update" pill (new Phast release) and the zoom indicator in the address bar.</summary>
public partial class MainWindow
{
    private void InitExtrasUi()
    {
        UpdateChecker.Available += OnUpdateAvailable;
        Closed += (_, _) => UpdateChecker.Available -= OnUpdateAvailable;
        if (UpdateChecker.Found is { } found) OnUpdateAvailable(found.Version, found.Url);
    }

    private void OnUpdateAvailable(string version, string url)
    {
        UpdateButton.Visibility = Visibility.Visible;
        UpdateButton.ToolTip = $"Hay una versión nueva de Phast ({version}). Haz clic para descargarla.";
        UpdateButton.Tag = url;
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateButton.Tag is string url) Tabs.OpenNextToActive(url, activate: true);
    }

    // ---------------------------------------------------------------- zoom indicator

    private void OnActiveZoomChanged(object? sender, EventArgs e) => UpdateZoomButton();

    private void UpdateZoomButton()
    {
        var zoom = Tabs.Active?.Zoom ?? 1;
        var zoomed = Math.Abs(zoom - 1) > 0.001;
        ZoomButton.Visibility = zoomed ? Visibility.Visible : Visibility.Collapsed;
        ZoomButton.Content = zoom > 1 ? "" : ""; // zoom in / zoom out magnifier
        ZoomButton.ToolTip = $"Zoom: {zoom:P0} (se recuerda para esta web). Haz clic para volver al 100 %.";
    }

    private void Zoom_Click(object sender, RoutedEventArgs e) => Tabs.Active?.ResetZoom();
}
