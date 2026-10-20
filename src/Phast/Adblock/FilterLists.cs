using System.IO;
using System.Net.Http;
using Phast.Core;

namespace Phast.Adblock;

public sealed record FilterList(string Id, string Name, string Url, string Description);

/// <summary>
/// Known filter lists, and downloading/caching them in %LOCALAPPDATA%\Phast\Filters.
/// The lists are not bundled with Phast: they are fetched from their official sites and used unmodified.
/// </summary>
public static class FilterLists
{
    public static readonly IReadOnlyList<FilterList> BuiltIn =
    [
        new("easylist", "EasyList", "https://easylist.to/easylist/easylist.txt", "Anuncios (lista principal)"),
        new("easyprivacy", "EasyPrivacy", "https://easylist.to/easylist/easyprivacy.txt", "Rastreadores y analítica"),
        new("easylist-es", "EasyList Spanish", "https://easylist-downloads.adblockplus.org/easylistspanish.txt", "Anuncios de webs en español"),
        new("cookies", "EasyList Cookie List", "https://secure.fanboy.co.nz/fanboy-cookiemonster.txt",
            "Oculta avisos de cookies (puede dejar alguna web sin poder aceptarlas)"),
    ];

    /// <summary>Lists are refreshed after this many days (EasyList's own "Expires" value).</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(4);

    private static string Folder => Path.Combine(AppPaths.Root, "Filters");
    public static string FileFor(string id) => Path.Combine(Folder, id + ".txt");

    public static FilterList? Get(string id) => BuiltIn.FirstOrDefault(l => l.Id == id);

    public static DateTime? LastUpdated(string id) =>
        File.Exists(FileFor(id)) ? File.GetLastWriteTime(FileFor(id)) : null;

    public static bool IsStale(string id) =>
        LastUpdated(id) is not DateTime t || DateTime.Now - t > MaxAge;

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var http = new HttpClient(new SocketsHttpHandler { AutomaticDecompression = System.Net.DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Phast (+https://github.com/xpedroic/phast)");
        return http;
    });

    /// <summary>Downloads a list. Returns true if a new version was saved.</summary>
    public static async Task<bool> DownloadAsync(FilterList list, CancellationToken ct = default)
    {
        try
        {
            var text = await Http.Value.GetStringAsync(list.Url, ct).ConfigureAwait(false);
            // Sanity check: a real filter list starts with a header and has many lines (not an error page).
            if (!text.TrimStart().StartsWith('[') && !text.TrimStart().StartsWith('!') || text.Length < 1000)
            {
                Log.Write($"Filter list {list.Id}: unexpected content, ignored");
                return false;
            }
            Directory.CreateDirectory(Folder);
            var tmp = FileFor(list.Id) + ".tmp";
            await File.WriteAllTextAsync(tmp, text, ct).ConfigureAwait(false);
            File.Move(tmp, FileFor(list.Id), overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            Log.Write($"Filter list {list.Id} download failed: {ex.Message}");
            // Keep the old copy, but mark it as checked so we don't retry on every start while offline.
            if (File.Exists(FileFor(list.Id))) File.SetLastWriteTime(FileFor(list.Id), DateTime.Now - MaxAge + TimeSpan.FromHours(6));
            return false;
        }
    }
}
