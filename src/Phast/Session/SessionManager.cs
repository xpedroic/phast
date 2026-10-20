using System.Windows;
using System.Windows.Threading;
using Phast.Settings;

namespace Phast.Session;

/// <summary>
/// Keeps track of open windows, saves the session shortly after any change (debounced)
/// and restores it at startup. Closing the last window exits the app.
/// </summary>
public static class SessionManager
{
    private static readonly List<MainWindow> _windows = [];
    private static DispatcherTimer? _saveTimer;
    private static bool _exiting;

    public static IReadOnlyList<MainWindow> Windows => _windows;
    public static MainWindow? LastActiveWindow { get; set; }

    /// <summary>Opens the startup windows (restored session and/or URLs from the command line).</summary>
    public static void Start(IReadOnlyList<string> urls)
    {
        var session = SettingsService.Current.Startup == StartupMode.RestoreSession ? SessionStore.Load() : new SessionData();
        var restored = session.Windows.Where(w => w.Tabs.Count > 0).ToList();

        if (restored.Count == 0)
        {
            OpenWindow(null, urls);
            return;
        }
        for (int i = 0; i < restored.Count; i++)
            OpenWindow(restored[i], i == restored.Count - 1 ? urls : null);
    }

    public static MainWindow OpenWindow(SessionWindow? restore = null, IEnumerable<string>? urls = null)
    {
        Core.Log.Write($"Startup: creating window at {Core.Log.SinceStart:F0} ms");
        var window = new MainWindow(restore, urls);
        Core.Log.Write($"Startup: window built at {Core.Log.SinceStart:F0} ms");
        _windows.Add(window);
        window.Closing += (_, _) => OnWindowClosing(window);
        window.Closed += (_, _) => OnWindowClosed(window);
        window.Show();
        window.Activate();
        LastActiveWindow = window;
        MarkDirty();
        return window;
    }

    /// <summary>Handles a second launch of Phast: open its URLs in the last window, or a new window.</summary>
    public static void HandleExternalLaunch(IReadOnlyList<string> urls)
    {
        var target = LastActiveWindow ?? _windows.LastOrDefault();
        if (urls.Count == 0 || target == null)
        {
            OpenWindow(null, urls);
            return;
        }
        foreach (var url in urls) target.Tabs.Open(url);
        if (target.WindowState == WindowState.Minimized) target.WindowState = WindowState.Normal;
        target.Activate();
    }

    public static void MarkDirty()
    {
        if (_exiting) return;
        _saveTimer ??= CreateTimer();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private static DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        t.Tick += (_, _) => { t.Stop(); SaveNow(); };
        return t;
    }

    public static void SaveNow() =>
        SessionStore.Save(new SessionData { Windows = _windows.Select(w => w.ToSession()).ToList() });

    private static void OnWindowClosing(MainWindow window)
    {
        _saveTimer?.Stop();
        // Closing the last window keeps it in the session; closing one of several drops it (like Chrome).
        if (_exiting || _windows.Count == 1) SaveNow();
        else
        {
            _windows.Remove(window);
            SaveNow();
        }
    }

    private static void OnWindowClosed(MainWindow window)
    {
        _windows.Remove(window);
        if (LastActiveWindow == window) LastActiveWindow = _windows.LastOrDefault();
        if (_windows.Count == 0) Application.Current.Shutdown();
    }

    /// <summary>"Exit" from the menu: save every window, then close them all.</summary>
    public static void ExitAll()
    {
        _saveTimer?.Stop();
        SaveNow();
        _exiting = true;
        foreach (var w in _windows.ToList()) w.Close();
    }
}
