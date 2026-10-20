using System.Windows;
using System.Windows.Media;
using Phast.Downloads;

namespace Phast;

/// <summary>Toolbar downloads button: appears with the first download and shows overall progress as a ring.</summary>
public partial class MainWindow
{
    // Circumference of the 26 px ring (24 px diameter at the stroke centre) in stroke-thickness units (2 px).
    private const double RingLength = Math.PI * 24 / 2;

    private void InitDownloadsUi()
    {
        DownloadManager.Changed += UpdateDownloadsButton;
        Closed += (_, _) => DownloadManager.Changed -= UpdateDownloadsButton;
    }

    private void UpdateDownloadsButton()
    {
        DownloadsButton.Visibility = Visibility.Visible;
        var progress = DownloadManager.OverallProgress;
        DownloadRing.Visibility = progress >= 0 ? Visibility.Visible : Visibility.Collapsed;
        if (progress >= 0) DownloadRing.StrokeDashArray = new DoubleCollection { Math.Max(0.5, progress * RingLength), RingLength };
        var active = DownloadManager.ActiveCount;
        DownloadsButton.ToolTip = active > 0
            ? $"Descargando {active} archivo{(active == 1 ? "" : "s")}" + (progress >= 0 ? $" · {progress:P0}" : "")
            : "Descargas (Ctrl+J)";
    }

    private void Downloads_Click(object sender, RoutedEventArgs e) => Execute(Input.Commands.Downloads);
}
