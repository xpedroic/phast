using System.Windows;
using System.Windows.Shell;

namespace Phast;

/// <summary>Full screen mode (F11, or a page's video going full screen): hides tabs and toolbar and covers the monitor.</summary>
public partial class MainWindow
{
    private bool _isFullScreen;
    private WindowState _stateBeforeFullScreen;
    private Rect _boundsBeforeFullScreen;

    public void SetFullScreen(bool on)
    {
        if (on == _isFullScreen) return;
        var chrome = WindowChrome.GetWindowChrome(this);

        if (on)
        {
            _stateBeforeFullScreen = WindowState;
            _boundsBeforeFullScreen = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            _isFullScreen = true;

            TitleBar.Visibility = Visibility.Collapsed;
            Toolbar.Visibility = Visibility.Collapsed;
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
            ResizeMode = ResizeMode.NoResize;

            if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
            var screen = _frame.GetMonitorBounds();
            Left = screen.Left; Top = screen.Top; Width = screen.Width; Height = screen.Height;
        }
        else
        {
            _isFullScreen = false;
            TitleBar.Visibility = Visibility.Visible;
            Toolbar.Visibility = Visibility.Visible;
            chrome.ResizeBorderThickness = new Thickness(6);
            ResizeMode = ResizeMode.CanResize;

            Left = _boundsBeforeFullScreen.Left; Top = _boundsBeforeFullScreen.Top;
            Width = _boundsBeforeFullScreen.Width; Height = _boundsBeforeFullScreen.Height;
            if (_stateBeforeFullScreen == WindowState.Maximized) WindowState = WindowState.Maximized;
            OnWindowStateChanged();

            // If a video was full screen, tell the page to leave it too.
            if (Tabs.Active?.Core?.ContainsFullScreenElement == true)
                _ = Tabs.Active.Core.ExecuteScriptAsync("document.exitFullscreen && document.exitFullscreen()");
        }
    }
}
