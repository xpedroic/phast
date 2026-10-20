using System.IO;
using System.Windows;
using System.Windows.Threading;
using Phast.Session;

namespace Phast.Core;

/// <summary>
/// Safety net: an unexpected error is written to %LOCALAPPDATA%\Phast\crashes, the session is saved and,
/// when possible, Phast keeps running instead of closing. If the web engine itself dies, Phast restarts
/// and the session brings every tab back.
/// </summary>
public static class CrashGuard
{
    private static DateTime _lastMessage = DateTime.MinValue;
    private static bool _restarting;

    public static string Folder => Path.Combine(AppPaths.Root, "crashes");

    public static void Install(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Report(e.Exception, "UI");
            e.Handled = true; // keep the browser alive
            Notify();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Report(ex, "fatal");
            try { SessionManager.SaveNow(); } catch { /* best effort while dying */ }
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Report(e.Exception, "background");
            e.SetObserved();
        };
    }

    public static void Report(Exception ex, string where)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var file = Path.Combine(Folder, $"error-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, $"Phast {typeof(CrashGuard).Assembly.GetName().Version} · {DateTime.Now:s} · {where}\n" +
                                    $"Windows {Environment.OSVersion.Version} · WebView2 {BrowserEnvironment.RuntimeVersion()}\n\n{ex}");
            // Keep only the last 20 reports.
            foreach (var old in new DirectoryInfo(Folder).GetFiles("error-*.txt").OrderByDescending(f => f.Name).Skip(20)) old.Delete();
            Log.Write($"Error ({where}): {ex}");
        }
        catch { /* never throw from the error handler */ }
        try { SessionManager.SaveNow(); } catch { }
    }

    private static void Notify()
    {
        if (DateTime.Now - _lastMessage < TimeSpan.FromMinutes(1)) return;
        _lastMessage = DateTime.Now;
        Application.Current?.Dispatcher.BeginInvoke(() =>
            MessageBox.Show(
                "Phast ha tenido un problema inesperado, pero sigue funcionando y tus pestañas están guardadas.\n\n" +
                $"Si se repite, puedes enviar el informe que hay en:\n{Folder}",
                "Phast", MessageBoxButton.OK, MessageBoxImage.Warning), DispatcherPriority.Background);
    }

    /// <summary>The engine's main process exited: restart Phast once (the saved session restores the tabs).</summary>
    public static void EngineLost()
    {
        if (_restarting) return;
        _restarting = true;
        Report(new InvalidOperationException("The WebView2 browser process exited."), "engine");
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            MessageBox.Show("El motor de páginas web se ha cerrado inesperadamente. Phast se va a reiniciar y recuperará tus pestañas.",
                "Phast", MessageBoxButton.OK, MessageBoxImage.Information);
            AppRestart.Restart();
        });
    }
}
