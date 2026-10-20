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

        // Restarting (to apply engine flags): wait until the previous Phast and its engine have closed.
        var restartArg = e.Args.FirstOrDefault(a => a.StartsWith(AppRestart.Argument, StringComparison.Ordinal));
        if (restartArg != null) AppRestart.WaitForPrevious(restartArg);

        if (!SingleInstance.TryAcquire(e.Args.Where(a => a != restartArg).ToArray()))
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
        Shortcuts = ShortcutMap.Create(SettingsService.Current.Shortcuts);
        SettingsService.Changed += () => Shortcuts = ShortcutMap.Create(SettingsService.Current.Shortcuts);
        _ = BrowserEnvironment.GetAsync();   // start the engine now, in parallel with the UI
        Adblock.AdblockService.Start();      // load filter lists in the background
        ThemeManager.Initialize();
        InternalPages.EnsureExtracted();
        Bridge.PageBridge.Initialize();
        Dark.DarkModeService.Initialize();
        Omnibox.SuggestionProvider.BookmarkSource = text => Bookmarks.BookmarkStore.Search(text).Take(3).Select(b => new Omnibox.Suggestion
        {
            Kind = Omnibox.SuggestionKind.Bookmark,
            Text = b.Title.Length > 0 ? b.Title : b.Url!,
            Detail = Omnibox.SuggestionProvider.Strip(b.Url!).TrimEnd('/'),
            Target = b.Url!,
            FillText = b.Url!,
        });

        SingleInstance.ArgumentsReceived += args =>
            Dispatcher.BeginInvoke(() => SessionManager.HandleExternalLaunch(ParseUrls(args)));

        SessionManager.Start(ParseUrls(e.Args));
        Tabs.TabSleeper.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        History.HistoryStore.Close();
        Bookmarks.BookmarkStore.SaveNow();
        base.OnExit(e);
    }

    /// <summary>Command-line arguments that are not switches are treated as URLs / files / searches.</summary>
    private static List<string> ParseUrls(string[] args) =>
        args.Where(a => !a.StartsWith('-'))
            .Select(UrlResolver.Resolve)
            .Where(u => u != null)
            .Select(u => u!)
            .ToList();
}
