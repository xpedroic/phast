using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Phast.Downloads;
using Phast.History;
using Phast.Settings;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>Messages of phast://history, phast://downloads and the "clear browsing data" dialog.</summary>
public static class HistoryBridge
{
    private static DispatcherTimer? _downloadsThrottle;

    public static void Initialize()
    {
        // Progress events are frequent: refresh open download pages at most ~3 times per second.
        DownloadManager.Changed += () =>
        {
            if (_downloadsThrottle == null)
            {
                _downloadsThrottle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
                _downloadsThrottle.Tick += (_, _) =>
                {
                    _downloadsThrottle.Stop();
                    PageBridge.Broadcast(DownloadManager.ToJson().ToJsonString(), "downloads.html");
                };
            }
            if (!_downloadsThrottle.IsEnabled) _downloadsThrottle.Start();
        };
    }

    public static async void Handle(BrowserTab page, string type, JsonElement msg)
    {
        string S(string name) => msg.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        try
        {
            switch (type)
            {
                case "history.get":
                    var before = msg.TryGetProperty("before", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt64() : 0;
                    var visits = await HistoryStore.VisitsAsync(S("query"), before);
                    page.PostMessage(new JsonObject
                    {
                        ["type"] = "history",
                        ["append"] = before > 0,
                        ["items"] = new JsonArray(visits.Select(v => (JsonNode)new JsonObject
                        {
                            ["id"] = v.Id, ["url"] = v.Url, ["title"] = v.Title,
                            ["time"] = new DateTimeOffset(v.Time).ToUnixTimeMilliseconds(),
                            ["icon"] = Core.FaviconCache.GetDataUrl(v.Url),
                        }).ToArray()),
                    }.ToJsonString());
                    break;
                case "history.delete":
                    var ids = msg.GetProperty("ids").EnumerateArray().Select(x => x.GetInt64()).ToList();
                    await HistoryStore.DeleteVisitsAsync(ids);
                    page.PostMessage("{\"type\":\"historyChanged\"}");
                    break;

                case "browsingData.clear":
                    await ClearBrowsingDataAsync(page, S("range"), msg.GetProperty("kinds").EnumerateArray().Select(k => k.GetString() ?? "").ToHashSet());
                    page.PostMessage(new JsonObject { ["type"] = "toast", ["text"] = "Datos de navegación borrados" }.ToJsonString());
                    page.PostMessage("{\"type\":\"historyChanged\"}");
                    break;

                case "downloads.get":
                    page.PostMessage(DownloadManager.ToJson().ToJsonString());
                    break;
                case "downloads.act":
                    DownloadManager.Act(S("id"), S("action"));
                    break;
                case "downloads.clear":
                    DownloadManager.ClearFinished();
                    break;
                case "downloads.chooseFolder":
                    var dialog = new Microsoft.Win32.OpenFolderDialog
                    {
                        Title = "Carpeta de descargas",
                        InitialDirectory = DownloadManager.DefaultFolder,
                    };
                    if (dialog.ShowDialog(Session.SessionManager.LastActiveWindow) == true)
                        SettingsService.Update(s => s.DownloadFolder = dialog.FolderName);
                    page.PostMessage(DownloadManager.ToJson().ToJsonString());
                    break;
            }
        }
        catch (Exception ex)
        {
            Core.Log.Write($"History/downloads message {type} failed: {ex.Message}");
        }
    }

    private static async Task ClearBrowsingDataAsync(BrowserTab page, string range, HashSet<string> kinds)
    {
        var since = range switch
        {
            "hour" => DateTime.Now.AddHours(-1),
            "day" => DateTime.Now.AddDays(-1),
            "week" => DateTime.Now.AddDays(-7),
            "month" => DateTime.Now.AddDays(-28),
            _ => DateTime.MinValue,
        };
        if (kinds.Contains("history")) await HistoryStore.ClearAsync(since);
        if (kinds.Contains("downloads")) DownloadManager.ClearFinished();

        var engineKinds = (CoreWebView2BrowsingDataKinds)0;
        if (kinds.Contains("cookies")) engineKinds |= CoreWebView2BrowsingDataKinds.AllSite;
        if (kinds.Contains("cache")) engineKinds |= CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage;
        if (kinds.Contains("passwords")) engineKinds |= CoreWebView2BrowsingDataKinds.PasswordAutosave | CoreWebView2BrowsingDataKinds.GeneralAutofill;
        if (engineKinds != 0 && page.Core != null)
        {
            var start = since == DateTime.MinValue ? new DateTime(1970, 1, 1) : since;
            await page.Core.Profile.ClearBrowsingDataAsync(engineKinds, start, DateTime.Now.AddMinutes(1));
        }
    }
}
