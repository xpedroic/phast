using System.Windows;
using Phast.Core;
using Phast.Input;
using Phast.Session;
using Phast.Settings;
using Phast.Themes;

namespace Phast;

/// <summary>
/// Application entry point. Startup is ordered for the fastest possible cold start:
/// the WebView2 engine starts loading in the background while the window is being built.
/// </summary>
public partial class App : Application
{
    public static ShortcutMap Shortcuts { get; private set; } = ShortcutMap.CreateDefault();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!SingleInstance.TryAcquire(e.Args))
        {
            Shutdown();
            return;
        }

        if (BrowserEnvironment.RuntimeVersion() == null)
        {
            MessageBox.Show(
                "Phast necesita el componente Microsoft Edge WebView2 Runtime, que no está instalado.\n\n" +
                "Descárgalo gratis desde https://developer.microsoft.com/microsoft-edge/webview2/",
                "Phast", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        AppPaths.EnsureCreated();
        SettingsService.Load();
        _ = BrowserEnvironment.GetAsync();   // start the engine now, in parallel with the UI
        ThemeManager.Initialize();
        InternalPages.EnsureExtracted();

        SingleInstance.ArgumentsReceived += args =>
            Dispatcher.BeginInvoke(() => SessionManager.HandleExternalLaunch(ParseUrls(args)));

        SessionManager.Start(ParseUrls(e.Args));
    }

    /// <summary>Command-line arguments that are not switches are treated as URLs / files / searches.</summary>
    private static List<string> ParseUrls(string[] args) =>
        args.Where(a => !a.StartsWith('-'))
            .Select(UrlResolver.Resolve)
            .Where(u => u != null)
            .Select(u => u!)
            .ToList();
}
