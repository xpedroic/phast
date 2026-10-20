using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using static Phast.Interop.NativeMethods;

namespace Phast.Interop;

/// <summary>
/// Glue between a WPF window with a custom (Chrome-like) title bar and the Windows frame:
/// - dark/light native border,
/// - correct margins when maximized (WindowChrome windows overflow the screen otherwise),
/// - Windows 11 Snap Layouts flyout when hovering our custom maximize button.
/// </summary>
public sealed class WindowFrame
{
    private readonly Window _window;
    private readonly FrameworkElement _root;
    private readonly Button _maximizeButton;
    private IntPtr _hwnd;
    private bool _maxHover;

    public WindowFrame(Window window, FrameworkElement root, Button maximizeButton)
    {
        _window = window;
        _root = root;
        _maximizeButton = maximizeButton;
        window.SourceInitialized += OnSourceInitialized;
        window.StateChanged += (_, _) => UpdateMaximizedMargin();
        window.DpiChanged += (_, _) => UpdateMaximizedMargin();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(_window).Handle;
        int round = DWMWCP_ROUND;
        DwmSetWindowAttribute(_hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        UpdateMaximizedMargin();
    }

    public void SetDarkFrame(bool dark)
    {
        if (_hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(_hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    /// <summary>Bounds (in WPF device-independent units) of the monitor the window is on.</summary>
    public Rect GetMonitorBounds()
    {
        var monitor = MonitorFromWindow(_hwnd, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(monitor, ref info);
        var scale = 96.0 / GetDpiForWindow(_hwnd);
        var r = info.rcMonitor;
        return new Rect(r.Left * scale, r.Top * scale, (r.Right - r.Left) * scale, (r.Bottom - r.Top) * scale);
    }

    /// <summary>
    /// A maximized WindowChrome window extends beyond the monitor by the resize-frame size;
    /// pad the content by that amount so nothing is cut off.
    /// </summary>
    private void UpdateMaximizedMargin()
    {
        if (_window.WindowState != WindowState.Maximized || _hwnd == IntPtr.Zero)
        {
            _root.Margin = new Thickness(0);
            return;
        }
        var dpi = GetDpiForWindow(_hwnd);
        var px = GetSystemMetricsForDpi(SM_CXSIZEFRAME, dpi) + GetSystemMetricsForDpi(SM_CXPADDEDBORDER, dpi);
        var dip = px * 96.0 / dpi;
        _root.Margin = new Thickness(dip);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_NCHITTEST:
                // Report our maximize button as the native one so Windows 11 shows Snap Layouts.
                if (IsOverMaximizeButton(lParam))
                {
                    SetMaxHover(true);
                    handled = true;
                    return HTMAXBUTTON;
                }
                SetMaxHover(false);
                break;

            case WM_NCLBUTTONDOWN:
                if (wParam == HTMAXBUTTON) { handled = true; }
                break;

            case WM_NCLBUTTONUP:
                if (wParam == HTMAXBUTTON)
                {
                    handled = true;
                    _window.WindowState = _window.WindowState == WindowState.Maximized
                        ? WindowState.Normal : WindowState.Maximized;
                }
                break;

            case WM_NCMOUSELEAVE:
            case WM_MOUSEMOVE:
                SetMaxHover(false);
                break;
        }
        return IntPtr.Zero;
    }

    private bool IsOverMaximizeButton(IntPtr lParam)
    {
        if (!_maximizeButton.IsVisible) return false;
        var screen = new Point(LowWord(lParam), HighWord(lParam));
        try
        {
            var local = _maximizeButton.PointFromScreen(screen);
            return local.X >= 0 && local.Y >= 0 && local.X < _maximizeButton.ActualWidth && local.Y < _maximizeButton.ActualHeight;
        }
        catch (InvalidOperationException)
        {
            return false; // not connected to a presentation source yet
        }
    }

    private void SetMaxHover(bool hover)
    {
        if (_maxHover == hover) return;
        _maxHover = hover;
        // The button never receives WPF mouse events while we claim HTMAXBUTTON, so drive its hover look manually.
        _maximizeButton.Tag = hover ? "hover" : null;
    }
}
