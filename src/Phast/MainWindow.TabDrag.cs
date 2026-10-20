using System.Windows;
using System.Windows.Input;
using Phast.Tabs;

namespace Phast;

/// <summary>Tab strip mouse handling: select, middle-click close, close/mute buttons and drag-to-reorder.</summary>
public partial class MainWindow
{
    private const double DragThreshold = 5;
    private BrowserTab? _dragTab;
    private Point _dragStart;
    private bool _dragging;

    private void InitTabDrag()
    {
        // Mouse capture goes to the whole list: moving a tab re-creates its visual, which would drop a per-tab capture.
        TabsList.MouseMove += TabsList_MouseMove;
        TabsList.MouseLeftButtonUp += (_, _) => EndDrag();
        TabsList.LostMouseCapture += (_, _) => EndDrag();
    }

    private static BrowserTab? TabOf(object sender) => (sender as FrameworkElement)?.DataContext as BrowserTab;

    private void Tab_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var tab = TabOf(sender);
        if (tab == null) return;
        Tabs.Activate(tab);
        _dragTab = tab;
        _dragStart = e.GetPosition(TabsList);
        _dragging = false;
        TabsList.CaptureMouse();
        e.Handled = true;
    }

    private void TabsList_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragTab == null || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(TabsList);
        if (!_dragging)
        {
            if (Math.Abs(pos.X - _dragStart.X) < DragThreshold) return;
            _dragging = true;
        }

        if (Tabs.Tabs.Count < 2) return;
        var target = TabIndexAt(pos.X);
        var from = Tabs.Tabs.IndexOf(_dragTab);
        if (from >= 0 && target >= 0 && target != from) Tabs.Move(from, target);
    }

    /// <summary>Index (in Tabs) of the visible tab under the given X position of the strip (group labels are skipped).</summary>
    private int TabIndexAt(double x)
    {
        var last = -1;
        foreach (var tab in Tabs.Tabs)
        {
            if (TabsList.ItemContainerGenerator.ContainerFromItem(tab) is not FrameworkElement c || !c.IsVisible) continue;
            var left = c.TranslatePoint(new Point(0, 0), TabsList).X;
            if (x < left + c.ActualWidth) return Tabs.Tabs.IndexOf(tab);
            last = Tabs.Tabs.IndexOf(tab);
        }
        return last;
    }

    private void EndDrag()
    {
        _dragTab = null;
        _dragging = false;
        if (TabsList.IsMouseCaptured) TabsList.ReleaseMouseCapture();
    }

    private void Tab_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && TabOf(sender) is { } tab)
        {
            Tabs.Close(tab);
            e.Handled = true;
        }
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        if (TabOf(sender) is { } tab) Tabs.Close(tab);
    }

    private void TabAudio_Click(object sender, RoutedEventArgs e) => TabOf(sender)?.ToggleMute();
}
