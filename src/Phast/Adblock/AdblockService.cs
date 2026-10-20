using System.Diagnostics;
using System.IO;
using Phast.Core;
using Phast.Settings;

namespace Phast.Adblock;

/// <summary>
/// Owns the active <see cref="FilterEngine"/>: builds it from the cached lists at startup (off the UI thread),
/// downloads missing/old lists in the background and swaps in a new engine when they change.
/// Also answers "is blocking on for this site?" (global switch + user allowlist).
/// </summary>
public static class AdblockService
{
    private static volatile FilterEngine? _engine;

    /// <summary>Current engine, or null while it is still being built (requests are then allowed).</summary>
    public static FilterEngine? Engine => _engine;

    public static DateTime? BuiltAt { get; private set; }
    public static TimeSpan BuildTime { get; private set; }
    public static bool Updating { get; private set; }

    /// <summary>Raised (on a background thread) when a new engine is active or the update state changes.</summary>
    public static event Action? StatusChanged;

    /// <summary>Called once at startup: load cached lists, then refresh stale ones. Never blocks the UI.</summary>
    public static void Start()
    {
        _ = Task.Run(async () =>
        {
            Rebuild();
            await UpdateAsync(force: false).ConfigureAwait(false);
            await SiteScripts.UpdateTwitchAsync().ConfigureAwait(false);
        });
    }

    /// <summary>Downloads lists that are missing or older than their expiry (or all of them when forced).</summary>
    public static async Task UpdateAsync(bool force)
    {
        var lists = SettingsService.Current.FilterLists.Select(FilterLists.Get).OfType<FilterList>().ToList();
        var toFetch = lists.Where(l => force || FilterLists.IsStale(l.Id)).ToList();
        if (toFetch.Count == 0) return;

        Updating = true;
        StatusChanged?.Invoke();
        var results = await Task.WhenAll(toFetch.Select(l => FilterLists.DownloadAsync(l))).ConfigureAwait(false);
        Updating = false;
        if (results.Any(r => r)) Rebuild();
        else StatusChanged?.Invoke();
    }

    /// <summary>Parses the enabled cached lists into a new engine and makes it active.</summary>
    public static void Rebuild()
    {
        lock (BuildLock)
        {
            BuildEngine();
        }
        StatusChanged?.Invoke();
    }

    private static readonly object BuildLock = new();

    private static void BuildEngine()
    {
        try
        {
            var sw = Stopwatch.StartNew();
            var engine = new FilterEngine();
            foreach (var id in SettingsService.Current.FilterLists)
            {
                var file = FilterLists.FileFor(id);
                if (File.Exists(file)) engine.AddList(File.ReadLines(file));
            }
            engine.Compile();
            BuildTime = sw.Elapsed;
            BuiltAt = DateTime.Now;
            _engine = engine;
            Log.Write($"Adblock engine: {engine.NetworkRuleCount} network + {engine.CosmeticRuleCount} cosmetic rules in {sw.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            Log.Write($"Adblock build failed: {ex}");
        }
    }

    /// <summary>The list selection changed in Settings: rebuild and fetch new lists.</summary>
    public static void OnListsChanged() => _ = Task.Run(async () =>
    {
        Rebuild();
        await UpdateAsync(force: false).ConfigureAwait(false);
    });

    /// <summary>Is blocking active on this site (global switch, user allowlist, lists' own site exceptions)?</summary>
    public static bool IsActiveFor(string? host)
    {
        var s = SettingsService.Current;
        if (!s.AdblockEnabled || string.IsNullOrEmpty(host)) return false;
        if (s.AdblockAllowlist.Any(d => NetRule.HostMatches(host, d))) return false;
        return _engine?.IsSiteExempt(host) != true;
    }

    /// <summary>Turns blocking on/off for a site (stored as its registrable domain, e.g. "youtube.com").</summary>
    public static void SetAllowed(string host, bool allowed)
    {
        var site = Domains.RegistrableDomain(host);
        SettingsService.Update(s =>
        {
            s.AdblockAllowlist.RemoveAll(d => NetRule.HostMatches(host, d) || d == site);
            if (allowed) s.AdblockAllowlist.Add(site);
        });
    }
}
