using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Phast.Bookmarks;
using Phast.Core;
using Phast.Session;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>
/// "bookmarks.*" messages from phast://bookmarks (manager) and phast://settings (import section).
/// </summary>
public static class BookmarksBridge
{
    public static void Initialize()
    {
        BookmarkStore.Changed += () => PageBridge.Broadcast(TreeMessage(), "bookmarks.html");
    }

    public static void Handle(BrowserTab tab, string type, JsonElement msg)
    {
        string S(string name) => msg.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        int? I(string name) => msg.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

        switch (type)
        {
            case "bookmarks.get":
                tab.PostMessage(TreeMessage());
                break;
            case "bookmarks.add":
                if (UrlResolver.Resolve(S("url")) is { } url)
                    BookmarkStore.Add(S("parentId"), S("title").Length > 0 ? S("title") : url, url, I("index"));
                break;
            case "bookmarks.addFolder":
                BookmarkStore.AddFolder(S("parentId"), S("title").Length > 0 ? S("title") : "Nueva carpeta", I("index"));
                break;
            case "bookmarks.update":
                BookmarkStore.Update(S("id"),
                    msg.TryGetProperty("title", out _) ? S("title") : null,
                    msg.TryGetProperty("url", out _) ? UrlResolver.Resolve(S("url")) : null);
                break;
            case "bookmarks.remove":
                BookmarkStore.Remove(S("id"));
                break;
            case "bookmarks.move":
                BookmarkStore.Move(S("id"), S("parentId"), I("index") ?? int.MaxValue);
                break;
            case "bookmarks.openAll":
                var window = SessionManager.LastActiveWindow;
                var folder = BookmarkStore.Find(S("id"));
                if (window != null && folder != null)
                    foreach (var u in folder.Descendants().Where(n => n.Url != null)) window.Tabs.Open(u.Url, activate: false);
                break;

            case "bookmarks.sources":
                var sources = BrowserImporter.Detect();
                tab.PostMessage(new JsonObject
                {
                    ["type"] = "bookmarkSources",
                    ["sources"] = new JsonArray(sources.Select(s => (JsonNode)new JsonObject
                    {
                        ["id"] = s.Id, ["label"] = s.Label, ["browser"] = s.Browser, ["count"] = s.Count,
                    }).ToArray()),
                }.ToJsonString());
                break;
            case "bookmarks.importBrowser":
                ImportBrowser(tab, S("id"));
                break;
            case "bookmarks.importHtml":
                ImportHtml(tab);
                break;
            case "bookmarks.exportHtml":
                ExportHtml(tab);
                break;
        }
    }

    private static void Reply(BrowserTab tab, string type, int count, string detail) =>
        tab.PostMessage(new JsonObject { ["type"] = type, ["count"] = count, ["detail"] = detail }.ToJsonString());

    /// <summary>The whole tree plus favicons (as data: URLs keyed by host).</summary>
    private static string TreeMessage()
    {
        var data = BookmarkStore.Data;
        var icons = new JsonObject();
        foreach (var n in data.Roots.SelectMany(r => r.Descendants()).Where(n => n.Url != null))
        {
            var host = FaviconCache.HostOf(n.Url);
            if (host == null || icons.ContainsKey(host)) continue;
            if (FaviconCache.GetDataUrl(n.Url) is { } dataUrl) icons[host] = dataUrl;
        }
        return new JsonObject
        {
            ["type"] = "bookmarks",
            ["bar"] = JsonSerializer.SerializeToNode(data.Bar, JsonFile.Options),
            ["other"] = JsonSerializer.SerializeToNode(data.Other, JsonFile.Options),
            ["icons"] = icons,
        }.ToJsonString();
    }

    private static async void ImportBrowser(BrowserTab tab, string id)
    {
        var source = BrowserImporter.Detect().FirstOrDefault(s => s.Id == id);
        if (source == null) { Reply(tab, "bookmarksImported", 0, "No se ha encontrado ese perfil."); return; }
        try { Reply(tab, "bookmarksImported", await BrowserImporter.ImportAsync(source), $"de {source.Browser}"); }
        catch (Exception ex) { Reply(tab, "bookmarksImported", 0, "No se pudo leer: " + ex.Message); }
    }

    private static void ImportHtml(BrowserTab tab)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar marcadores desde un archivo HTML",
            Filter = "Archivo de marcadores (*.html;*.htm)|*.html;*.htm|Todos los archivos|*.*",
        };
        if (dialog.ShowDialog(SessionManager.LastActiveWindow) != true) return;
        try
        {
            var count = HtmlBookmarks.Import(File.ReadAllText(dialog.FileName));
            Reply(tab, "bookmarksImported", count, $"de {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex)
        {
            Reply(tab, "bookmarksImported", 0, "No se pudo leer el archivo: " + ex.Message);
        }
    }

    private static void ExportHtml(BrowserTab tab)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar marcadores",
            FileName = $"marcadores_{DateTime.Now:yyyy_MM_dd}.html",
            Filter = "Archivo de marcadores (*.html)|*.html",
        };
        if (dialog.ShowDialog(SessionManager.LastActiveWindow) != true) return;
        File.WriteAllText(dialog.FileName, HtmlBookmarks.Export(BookmarkStore.Data));
        Reply(tab, "bookmarksExported", BookmarkStore.Data.Roots.Sum(r => r.Descendants().Count(n => n.Url != null)), dialog.FileName);
    }
}
