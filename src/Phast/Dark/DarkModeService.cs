using System.IO;
using System.Text.Json;
using Phast.Adblock;
using Phast.Core;
using Phast.Settings;

namespace Phast.Dark;

/// <summary>
/// Instant dark mode for web pages, powered by Dark Reader (MIT, bundled in Pages/scripts/darkreader.js).
/// Remembers the choice per site (registrable domain, so "m.youtube.com" follows "youtube.com").
/// </summary>
public static class DarkModeService
{
    /// <summary>Virtual host used by Dark Reader to read cross-origin stylesheets through Phast (see BrowserTab.DarkMode).</summary>
    public const string CssProxyHost = "phast-css.internal";

    private static string? _library;
    private static string _snapshot = "";

    /// <summary>Raised when the dark mode choices (sites, "all sites") change — not for other settings.</summary>
    public static event Action? Changed;

    public static void Initialize()
    {
        _snapshot = Snapshot();
        SettingsService.Changed += () =>
        {
            var now = Snapshot();
            if (now == _snapshot) return;
            _snapshot = now;
            Changed?.Invoke();
        };
    }

    private static string Snapshot()
    {
        var s = SettingsService.Current;
        return $"{s.DarkAllSites}|{string.Join(',', s.DarkSites)}|{string.Join(',', s.DarkExcludedSites)}";
    }

    public static string SiteOf(string host) => Domains.RegistrableDomain(host.ToLowerInvariant());

    /// <summary>Should pages of this host be dark?</summary>
    public static bool IsWanted(string? host)
    {
        if (string.IsNullOrEmpty(host) || host == InternalPages.Host) return false;
        var s = SettingsService.Current;
        var site = SiteOf(host);
        return s.DarkAllSites ? !s.DarkExcludedSites.Contains(site) : s.DarkSites.Contains(site);
    }

    /// <summary>Is dark mode used anywhere (otherwise tabs don't need the script at all)?</summary>
    public static bool AnyWanted => SettingsService.Current.DarkAllSites || SettingsService.Current.DarkSites.Count > 0;

    /// <summary>Turns dark mode on/off for a site and returns the new state.</summary>
    public static bool Toggle(string host)
    {
        var site = SiteOf(host);
        var on = !IsWanted(host);
        SettingsService.Update(s =>
        {
            s.DarkSites.Remove(site);
            s.DarkExcludedSites.Remove(site);
            if (s.DarkAllSites) { if (!on) s.DarkExcludedSites.Add(site); }
            else if (on) s.DarkSites.Add(site);
        });
        return on;
    }

    private static string Library => _library ??= File.ReadAllText(Path.Combine(AppPaths.Pages, "scripts", "darkreader.js"));

    /// <summary>Loads Dark Reader (once per page) and switches it on.</summary>
    private static string EnableSnippet => $$"""
        if (!window.DarkReader) {
          // Hide AMD/CommonJS loaders of the page so the library registers itself as window.DarkReader.
          const saved = [window.define, window.module, window.exports];
          window.define = window.module = window.exports = undefined;
          try { {{Library}} } finally { [window.define, window.module, window.exports] = saved; }
        }
        const phastStartDark = () => {
          try {
            window.DarkReader.setFetchMethod((url) => fetch('https://{{CssProxyHost}}/css?u=' + encodeURIComponent(url)));
            window.DarkReader.enable({ brightness: 100, contrast: 100, sepia: 0 });
          } catch (e) { window.__phastDarkError = String(e && e.stack || e); }
        };
        // At document start even <html> doesn't exist yet: wait for it (a few ms, still before the first paint).
        if (document.documentElement) phastStartDark();
        else {
          const waitForRoot = new MutationObserver(() => {
            if (!document.documentElement) return;
            waitForRoot.disconnect();
            phastStartDark();
          });
          waitForRoot.observe(document, { childList: true });
        }
        """;

    /// <summary>Script to run right now on the current page to switch dark mode on or off without reloading.</summary>
    public static string InstantScript(bool on) =>
        on ? $"(() => {{ if (window.top !== window) return; {EnableSnippet} }})();"
           : "(() => { if (window.DarkReader) window.DarkReader.disable(); })();";

    /// <summary>
    /// Script registered at document start in a tab: applies dark mode before the page paints, but only on the
    /// sites chosen at the time of registration (tabs re-register when the choice changes).
    /// </summary>
    public static string DocumentStartScript()
    {
        var s = SettingsService.Current;
        var sites = JsonSerializer.Serialize(s.DarkSites);
        var excluded = JsonSerializer.Serialize(s.DarkExcludedSites);
        return $$"""
            (() => {
              if (window.top !== window || !/^https?:$/.test(location.protocol) || location.hostname === '{{InternalPages.Host}}') return;
              const host = location.hostname.toLowerCase();
              const onSite = (list) => list.some((d) => host === d || host.endsWith('.' + d));
              const wanted = {{(s.DarkAllSites ? "true" : "false")}} ? !onSite({{excluded}}) : onSite({{sites}});
              if (!wanted) return;
              {{EnableSnippet}}
            })();
            """;
    }
}
