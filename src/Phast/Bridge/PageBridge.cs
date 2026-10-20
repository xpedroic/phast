using System.Text.Json;
using System.Text.Json.Nodes;
using Phast.Core;
using Phast.Session;
using Phast.Settings;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>
/// Handles messages sent by Phast's internal pages (phast://newtab, phast://settings...) through
/// window.chrome.webview.postMessage. Messages are JSON objects: { "type": "...", ...payload }.
/// Only pages served from https://phast.internal reach this class (checked in BrowserTab).
/// Replies are posted back as { "type": "...", ... } and received with phast.on(type, cb).
/// </summary>
public static class PageBridge
{
    public static void Initialize()
    {
        // Keep every open settings page in sync (e.g. theme changed from another window).
        SettingsService.Changed += () => Broadcast(SettingsMessage(), "settings.html");
        BookmarksBridge.Initialize();
        AdblockBridge.Initialize();
        HistoryBridge.Initialize();
    }

    public static void Handle(BrowserTab tab, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var type = root.GetProperty("type").GetString();
            switch (type)
            {
                case "navigate":
                    var url = UrlResolver.Resolve(root.GetProperty("text").GetString() ?? "");
                    if (url != null) tab.Navigate(url);
                    break;

                case "settings.get":
                    tab.PostMessage(SettingsMessage());
                    break;

                case "settings.set":
                    SettingsService.SetValue(root.GetProperty("key").GetString() ?? "", root.GetProperty("value"));
                    break;

                case not null when type.StartsWith("bookmarks.", StringComparison.Ordinal):
                    BookmarksBridge.Handle(tab, type, root);
                    break;

                case not null when type.StartsWith("dev.", StringComparison.Ordinal) || type.StartsWith("monitor.", StringComparison.Ordinal)
                                   || type.StartsWith("tab.", StringComparison.Ordinal) || type.StartsWith("shortcuts.", StringComparison.Ordinal)
                                   || type.StartsWith("site.", StringComparison.Ordinal):
                    DeveloperBridge.Handle(tab, type, root);
                    break;

                case not null when type.StartsWith("history.", StringComparison.Ordinal) || type.StartsWith("downloads.", StringComparison.Ordinal)
                                   || type.StartsWith("browsingData.", StringComparison.Ordinal):
                    HistoryBridge.Handle(tab, type, root);
                    break;

                case not null when type.StartsWith("adblock.", StringComparison.Ordinal):
                    AdblockBridge.Handle(tab, type, root);
                    break;

                case "groups.open":
                {
                    var id = root.GetProperty("id").GetString();
                    var pg = SettingsService.Current.PageGroups.FirstOrDefault(g => g.Id == id);
                    if (pg != null) WindowOf(tab)?.Tabs.OpenPageGroup(pg);
                    break;
                }

                case "groups.currentTabs":
                {
                    var urls = WindowOf(tab)?.Tabs.Tabs.Select(t => t.Url)
                        .Where(u => u.Length > 0 && !InternalPages.IsInternal(u)).Distinct().ToList() ?? [];
                    tab.PostMessage(new JsonObject
                    {
                        ["type"] = "currentTabs",
                        ["urls"] = new JsonArray(urls.Select(u => (JsonNode)u!).ToArray()),
                    }.ToJsonString());
                    break;
                }

                case "dark.remove":
                    var site = root.GetProperty("site").GetString() ?? "";
                    SettingsService.Update(s => { s.DarkSites.Remove(site); s.DarkExcludedSites.Remove(site); });
                    break;

                case "openPage":
                    // Internal pages asking to open another internal page (e.g. settings -> bookmarks manager).
                    var page = root.GetProperty("url").GetString() ?? "";
                    if (page.StartsWith(InternalPages.Scheme, StringComparison.Ordinal))
                        SessionManager.LastActiveWindow?.OpenInternalPage(page);
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"Unknown page message: {type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Bad page message: {ex.Message}");
        }
    }

    private static string SettingsMessage()
    {
        var msg = new JsonObject
        {
            ["type"] = "settings",
            ["settings"] = SettingsService.ToJson(),
            ["engines"] = new JsonArray(SearchEngines.All
                .Select(e => (JsonNode)new JsonObject { ["id"] = e.Id, ["name"] = e.Name }).ToArray()),
            ["version"] = typeof(PageBridge).Assembly.GetName().Version?.ToString(3),
            ["engineVersion"] = BrowserEnvironment.RuntimeVersion(),
        };
        return msg.ToJsonString();
    }

    /// <summary>The window that contains this tab.</summary>
    private static MainWindow? WindowOf(BrowserTab tab) =>
        SessionManager.Windows.FirstOrDefault(w => w.Tabs.Tabs.Contains(tab)) ?? SessionManager.LastActiveWindow;

    /// <summary>Sends a message to every loaded internal page (optionally only pages whose file name matches).</summary>
    public static void Broadcast(string json, string? page = null)
    {
        foreach (var window in SessionManager.Windows)
            foreach (var tab in window.Tabs.Tabs)
                if (tab.Core != null && InternalPages.IsInternal(tab.Url)
                    && (page == null || tab.Url.Contains("/" + page, StringComparison.OrdinalIgnoreCase)))
                    tab.PostMessage(json);
    }
}
