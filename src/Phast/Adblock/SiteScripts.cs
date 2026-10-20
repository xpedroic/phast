using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using Phast.Core;
using Phast.Settings;

namespace Phast.Adblock;

/// <summary>A script injected at document start into one family of sites (e.g. the Twitch ad blocker).</summary>
public sealed record SiteScript(string Id, string Name, string[] Domains, Func<bool> Enabled, Func<string> LoadSource);

/// <summary>
/// Built-in replacements for the ad-blocking extensions Phast doesn't support (Phast has no extensions by design):
/// - Twitch: "vaft" from TwitchAdSolutions (MIT, maintained fork by ryanbr), bundled and kept up to date;
/// - YouTube: Phast's own script (Pages/scripts/youtube-adblock.js).
/// A script is only registered in a tab the first time that tab visits one of its sites, so other sites pay nothing.
/// </summary>
public static partial class SiteScripts
{
    public const string TwitchUpdateUrl = "https://raw.githubusercontent.com/ryanbr/TwitchAdSolutions/master/vaft/vaft.user.js";
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromDays(2);

    private static string BundledFolder => Path.Combine(AppPaths.Pages, "scripts");
    private static string UpdatedFolder => Path.Combine(AppPaths.Root, "Scripts");
    private static string UpdatedTwitchFile => Path.Combine(UpdatedFolder, "twitch-vaft.js");

    public static readonly IReadOnlyList<SiteScript> All =
    [
        new("twitch", "Twitch", ["twitch.tv"],
            () => SettingsService.Current.TwitchAdblock, LoadTwitch),
        new("youtube", "YouTube", ["youtube.com", "youtube-nocookie.com"],
            () => SettingsService.Current.YouTubeAdblock, () => File.ReadAllText(Path.Combine(BundledFolder, "youtube-adblock.js"))),
    ];

    /// <summary>Scripts that should run on this host right now.</summary>
    public static IEnumerable<SiteScript> For(string host) =>
        All.Where(s => s.Domains.Any(d => NetRule.HostMatches(host, d)) && s.Enabled() && AdblockService.IsActiveFor(host));

    // ---------------------------------------------------------------- Twitch (vaft)

    [GeneratedRegex(@"@version\s+([\d.]+)")]
    private static partial Regex VersionRegex();

    public static Version? VersionOf(string script) =>
        VersionRegex().Match(script) is { Success: true } m && Version.TryParse(m.Groups[1].Value, out var v) ? v : null;

    /// <summary>The newest available vaft (downloaded update if newer than the bundled copy), guarded to Twitch hosts.</summary>
    private static string LoadTwitch()
    {
        var bundled = File.ReadAllText(Path.Combine(BundledFolder, "twitch-vaft.js"));
        var source = bundled;
        if (File.Exists(UpdatedTwitchFile))
        {
            var updated = File.ReadAllText(UpdatedTwitchFile);
            if (VersionOf(updated) > VersionOf(bundled)) source = updated;
        }
        // Registered scripts run in every document of the tab afterwards, so make sure it only acts on Twitch.
        return "if (/(^|\\.)twitch\\.tv$/.test(location.hostname)) {\n" + source + "\n}";
    }

    public static Version? TwitchVersion()
    {
        try { return VersionOf(LoadTwitch()); }
        catch (IOException) { return null; }
    }

    /// <summary>Downloads a newer vaft if the copy we have is older than a couple of days (runs in the background).</summary>
    public static async Task UpdateTwitchAsync(bool force = false)
    {
        if (!SettingsService.Current.TwitchAdblock || !SettingsService.Current.SiteScriptsAutoUpdate) return;
        if (!force && File.Exists(UpdatedTwitchFile) && DateTime.Now - File.GetLastWriteTime(UpdatedTwitchFile) < UpdateEvery) return;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var text = await http.GetStringAsync(TwitchUpdateUrl).ConfigureAwait(false);
            // Only accept something that really is vaft.
            if (!text.Contains("TwitchAdSolutions (vaft)") || VersionOf(text) == null || text.Length < 20_000)
            {
                Log.Write("Twitch script update: unexpected content, ignored");
                return;
            }
            Directory.CreateDirectory(UpdatedFolder);
            await File.WriteAllTextAsync(UpdatedTwitchFile, text).ConfigureAwait(false);
            Log.Write($"Twitch script updated to {VersionOf(text)}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            Log.Write($"Twitch script update failed: {ex.Message}");
        }
    }
}
