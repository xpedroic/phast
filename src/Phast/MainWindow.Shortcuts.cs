using System.Windows.Input;
using Phast.Input;
using Phast.Session;

namespace Phast;

/// <summary>Keyboard shortcuts and the commands they trigger.</summary>
public partial class MainWindow
{
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Alt combinations arrive as Key.System; WebView2 forwards its accelerator keys here as well.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var command = App.Shortcuts.Find(key, Keyboard.Modifiers);
        if (command != null && Execute(command)) e.Handled = true;
    }

    /// <summary>Switches to a tab already showing the internal page, or opens it in a new tab.</summary>
    public void OpenInternalPage(string phastUrl)
    {
        var real = Core.InternalPages.ToRealUrl(phastUrl);
        var page = real.Split('#', '?')[0];
        var existing = Tabs.Tabs.FirstOrDefault(t => t.Url.StartsWith(page, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            Tabs.Activate(existing);
            if (real != page) existing.Navigate(real);
        }
        else Tabs.OpenNextToActive(real, activate: true);
    }

    /// <summary>Runs a browser command. Returns false if it does not apply right now.</summary>
    public bool Execute(string command)
    {
        var tab = Tabs.Active;
        switch (command)
        {
            case Commands.NewTab: Tabs.Open(null); return true;
            case Commands.NewWindow: SessionManager.OpenWindow(); return true;
            case Commands.CloseTab: Tabs.CloseActive(); return true;
            case Commands.ReopenTab: Tabs.ReopenClosed(); return true;
            case Commands.NextTab: Tabs.ActivateRelative(+1); return true;
            case Commands.PreviousTab: Tabs.ActivateRelative(-1); return true;
            case Commands.LastTab: Tabs.ActivateLast(); return true;
            case Commands.FocusAddressBar:
                if (_isFullScreen) return false;
                FocusAddressBar();
                return true;
            case Commands.Reload: tab?.Core?.Reload(); return true;
            case Commands.HardReload:
                // Bypass the cache: ask the engine to reload ignoring cached resources.
                _ = tab?.Core?.CallDevToolsProtocolMethodAsync("Page.reload", "{\"ignoreCache\":true}");
                return true;
            case Commands.Back:
                if (tab?.CanGoBack == true) tab.Core?.GoBack();
                return true;
            case Commands.Forward:
                if (tab?.CanGoForward == true) tab.Core?.GoForward();
                return true;
            case Commands.DevTools: tab?.Core?.OpenDevToolsWindow(); return true;
            case Commands.FullScreen: SetFullScreen(!_isFullScreen); return true;
            case Commands.Print: tab?.Core?.ShowPrintUI(); return true;
            case Commands.Settings: OpenInternalPage("phast://settings"); return true;
            case Commands.Bookmark: BookmarkCurrentPage(); return true;
            case Commands.ToggleBookmarksBar:
                Settings.SettingsService.Update(s => s.ShowBookmarksBar = !s.ShowBookmarksBar);
                return true;
            case Commands.BookmarkManager: OpenInternalPage("phast://bookmarks"); return true;
            case Commands.DarkMode: ToggleDarkMode(); return true;
            case Commands.Home:tab?.Navigate(Core.InternalPages.ToRealUrl(Core.InternalPages.NewTab)); return true;
        }

        if (command.StartsWith("tab", StringComparison.Ordinal) && int.TryParse(command[3..], out var n))
        {
            Tabs.ActivateIndex(n - 1);
            return true;
        }
        return false;
    }
}
