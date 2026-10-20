using System.Text.Json;
using Phast.Core;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>
/// Handles messages sent by Phast's internal pages (phast://newtab, phast://settings...) through
/// window.chrome.webview.postMessage. Messages are JSON objects: { "type": "...", ...payload }.
/// Only pages served from https://phast.internal reach this class (checked in BrowserTab).
/// </summary>
public static class PageBridge
{
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
                    var text = root.GetProperty("text").GetString() ?? "";
                    var url = UrlResolver.Resolve(text);
                    if (url != null) tab.Navigate(url);
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
}
