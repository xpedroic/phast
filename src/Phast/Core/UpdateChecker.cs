using System.IO;
using System.Net.Http;
using System.Text.Json;
using Phast.Settings;

namespace Phast.Core;

/// <summary>
/// Once a day, asks GitHub whether a newer Phast release exists. Nothing is downloaded or installed:
/// the window just shows an "Actualizar" button that opens the release page.
/// </summary>
public static class UpdateChecker
{
    public const string ReleasesApi = "https://api.github.com/repos/xpedroic/phast/releases/latest";
    private static string StampFile => Path.Combine(AppPaths.Root, "update-check.txt");

    public static Version Current => typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>Newer version found (version text, release page URL). Raised on the UI thread.</summary>
    public static event Action<string, string>? Available;

    public static (string Version, string Url)? Found { get; private set; }

    /// <summary>Starts the daily check a little after startup (never slows down opening Phast).</summary>
    public static void Start(System.Windows.Threading.Dispatcher dispatcher)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(20));
            if (!SettingsService.Current.CheckForUpdates) return;
            if (File.Exists(StampFile) && DateTime.Now - File.GetLastWriteTime(StampFile) < TimeSpan.FromHours(20)) return;
            await CheckAsync(dispatcher);
        });
    }

    /// <summary>Checks now. Returns the newer version found, or null.</summary>
    public static async Task<string?> CheckAsync(System.Windows.Threading.Dispatcher dispatcher)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Phast/" + Current);
            using var response = await http.GetAsync(ReleasesApi);
            File.WriteAllText(StampFile, DateTime.Now.ToString("s"));
            if (!response.IsSuccessStatusCode) return null; // e.g. no release published yet
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.GetProperty("html_url").GetString() ?? "https://github.com/xpedroic/phast/releases";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest) || latest <= Current) return null;
            Found = (tag, url);
            await dispatcher.InvokeAsync(() => Available?.Invoke(tag, url));
            return tag;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException or KeyNotFoundException)
        {
            Log.Write($"Update check failed: {ex.Message}");
            return null;
        }
    }
}
