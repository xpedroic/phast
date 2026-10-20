using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Phast.Core;

namespace Phast.Bookmarks;

/// <summary>A browser profile that has a bookmarks file we can import.</summary>
public sealed record ImportSource(string Browser, string ProfileDir, string ProfileName, string BookmarksFile, int Count)
{
    public string Id => $"{Browser}|{Path.GetFileName(ProfileDir)}";
    public string Label => $"{Browser} — {ProfileName} ({Count} marcadores)";
}

/// <summary>
/// Imports bookmarks (and their favicons) from Chromium-based browsers: Chrome, Edge, Brave, Vivaldi.
/// They all store bookmarks in a JSON file called "Bookmarks" inside each profile folder,
/// and favicons in an SQLite database called "Favicons".
/// </summary>
public static class BrowserImporter
{
    private static readonly (string Name, string Folder)[] Browsers =
    [
        ("Chrome", @"Google\Chrome\User Data"),
        ("Edge", @"Microsoft\Edge\User Data"),
        ("Brave", @"BraveSoftware\Brave-Browser\User Data"),
        ("Vivaldi", @"Vivaldi\User Data"),
    ];

    /// <summary>Finds every installed browser profile with bookmarks.</summary>
    public static List<ImportSource> Detect()
    {
        var result = new List<ImportSource>();
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var (name, folder) in Browsers)
        {
            var userData = Path.Combine(local, folder);
            if (!Directory.Exists(userData)) continue;
            var names = ReadProfileNames(userData);
            foreach (var dir in Directory.EnumerateDirectories(userData))
            {
                var file = Path.Combine(dir, "Bookmarks");
                if (!File.Exists(file)) continue;
                var dirName = Path.GetFileName(dir);
                var count = 0;
                try { count = ParseFile(file).Sum(r => r.Descendants().Count(n => n.Url != null)); } catch { }
                result.Add(new ImportSource(name, dir, names.GetValueOrDefault(dirName, dirName), file, count));
            }
        }
        return result;
    }

    /// <summary>Profile display names live in "Local State" → profile.info_cache.</summary>
    private static Dictionary<string, string> ReadProfileNames(string userData)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(userData, "Local State")));
            if (doc.RootElement.TryGetProperty("profile", out var p) && p.TryGetProperty("info_cache", out var cache))
                foreach (var prof in cache.EnumerateObject())
                    if (prof.Value.TryGetProperty("name", out var n) && n.GetString() is { Length: > 0 } s)
                        names[prof.Name] = s;
        }
        catch { /* names are optional */ }
        return names;
    }

    /// <summary>Returns (bar, other, synced) folders from a Chromium "Bookmarks" file.</summary>
    private static List<BookmarkNode> ParseFile(string file)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var roots = doc.RootElement.GetProperty("roots");
        var list = new List<BookmarkNode>();
        foreach (var key in new[] { "bookmark_bar", "other", "synced" })
            list.Add(roots.TryGetProperty(key, out var r) ? Convert(r) : BookmarkNode.Folder(key));
        return list;
    }

    private static BookmarkNode Convert(JsonElement e)
    {
        var title = e.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        if (e.TryGetProperty("type", out var t) && t.GetString() == "url")
            return BookmarkNode.Link(title, e.GetProperty("url").GetString() ?? "");
        var folder = BookmarkNode.Folder(title);
        if (e.TryGetProperty("children", out var children))
            foreach (var c in children.EnumerateArray()) folder.Children!.Add(Convert(c));
        return folder;
    }

    /// <summary>
    /// Imports a profile. Bar items go straight onto Phast's bar when it's empty (or into a folder
    /// "Importado de X" otherwise); "Other" and mobile bookmarks go into a folder under "Otros marcadores".
    /// Returns the number of bookmarks imported.
    /// </summary>
    public static async Task<int> ImportAsync(ImportSource source)
    {
        // Reading files and copying icons runs in the background; only the final insert touches the UI thread.
        var (roots, urls) = await Task.Run(() =>
        {
            var parsed = ParseFile(source.BookmarksFile);
            var list = parsed.SelectMany(r => r.Descendants()).Where(x => x.Url != null).Select(x => x.Url!).ToList();
            // Icons first, so the bookmarks bar shows them as soon as the bookmarks appear.
            ImportFavicons(Path.Combine(source.ProfileDir, "Favicons"), list);
            return (parsed, list);
        });

        var (bar, other, synced) = (roots[0], roots[1], roots[2]);
        var label = $"Importado de {source.Browser}";

        if (BookmarkStore.Data.Bar.Children!.Count == 0)
            foreach (var c in bar.Children!) BookmarkStore.Insert(BookmarkData.BarId, c);
        else if (bar.Children!.Count > 0)
            BookmarkStore.Insert(BookmarkData.BarId, WithTitle(bar, label));

        var rest = BookmarkNode.Folder(label);
        rest.Children!.AddRange(other.Children!);
        if (synced.Children!.Count > 0) rest.Children.Add(WithTitle(synced, "Móvil"));
        if (rest.Children.Count > 0) BookmarkStore.Insert(BookmarkData.OtherId, rest);
        return urls.Count;
    }

    private static BookmarkNode WithTitle(BookmarkNode folder, string title)
    {
        folder.Title = title;
        return folder;
    }

    /// <summary>
    /// Copies favicons of the imported pages from the browser's Favicons database into Phast's cache.
    /// The database is copied first because the browser may have it open (locked).
    /// </summary>
    private static void ImportFavicons(string faviconsDb, List<string> urls)
    {
        if (!File.Exists(faviconsDb) || urls.Count == 0) return;
        var temp = Path.Combine(Path.GetTempPath(), $"phast-favicons-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(faviconsDb, temp, overwrite: true);
            using var db = new SqliteConnection($"Data Source={temp};Mode=ReadOnly;Pooling=False");
            db.Open();
            var wanted = new HashSet<string>(urls.Select(u => FaviconCache.HostOf(u)).OfType<string>(), StringComparer.OrdinalIgnoreCase);

            // Pass 1 (no image data, fast): pick for each wanted site the smallest bitmap that is at least 16px.
            var best = new Dictionary<string, (long Id, int Width, string Url)>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = db.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT m.page_url, b.id, b.width
                    FROM icon_mapping m JOIN favicon_bitmaps b ON b.icon_id = m.icon_id
                    WHERE b.width >= 16;
                    """;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    var url = r.GetString(0);
                    var host = FaviconCache.HostOf(url);
                    if (host == null || !wanted.Contains(host)) continue;
                    var width = r.GetInt32(2);
                    if (!best.TryGetValue(host, out var cur) || width < cur.Width) best[host] = (r.GetInt64(1), width, url);
                }
            }

            // Pass 2: read only the chosen images.
            foreach (var (_, (id, _, url)) in best)
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "SELECT image_data FROM favicon_bitmaps WHERE id = $id;";
                cmd.Parameters.AddWithValue("$id", id);
                if (cmd.ExecuteScalar() is byte[] png)
                    FaviconCache.Save(url, png, overwrite: false, notify: false);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Favicon import failed: {ex.Message}");
        }
        finally
        {
            try { File.Delete(temp); } catch { }
        }
    }
}
