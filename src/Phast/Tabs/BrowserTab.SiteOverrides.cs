using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Phast.Adblock;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>Developer per-site tweaks (Settings > Advanced > Per site): JavaScript off, user agent, custom CSS/JS.</summary>
public sealed partial class BrowserTab
{
    private string _defaultUserAgent = "";
    private SiteOverride? _currentOverride;

    public static SiteOverride? OverrideFor(string? host)
    {
        if (string.IsNullOrEmpty(host)) return null;
        var all = SettingsService.Current.SiteOverrides;
        if (all.Count == 0) return null;
        // Exact host first ("mail.example.com"), then the site ("example.com").
        return all.GetValueOrDefault(host.ToLowerInvariant()) ?? all.GetValueOrDefault(Domains.RegistrableDomain(host.ToLowerInvariant()));
    }

    private void InitSiteOverrides(CoreWebView2 core)
    {
        _defaultUserAgent = core.Settings.UserAgent;
        core.NavigationStarting += (_, e) =>
        {
            var host = Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) ? u.Host : null;
            _currentOverride = OverrideFor(host);
            // These settings apply to the navigation that is starting.
            var jsOn = _currentOverride?.JavaScriptDisabled != true;
            if (core.Settings.IsScriptEnabled != jsOn) core.Settings.IsScriptEnabled = jsOn;
            var ua = string.IsNullOrWhiteSpace(_currentOverride?.UserAgent) ? _defaultUserAgent : _currentOverride!.UserAgent;
            if (core.Settings.UserAgent != ua) core.Settings.UserAgent = ua;
        };
        core.DOMContentLoaded += (_, _) => ApplySiteCssJs(core);
    }

    private void ApplySiteCssJs(CoreWebView2 core)
    {
        var o = _currentOverride;
        if (o == null) return;
        if (o.Css.Length > 0)
            _ = core.ExecuteScriptAsync($$"""
                (() => { const s = document.createElement('style'); s.id = 'phast-site-css';
                  s.textContent = {{JsonSerializer.Serialize(o.Css)}}; (document.head || document.documentElement).appendChild(s); })();
                """);
        if (o.Js.Length > 0 && !o.JavaScriptDisabled)
            _ = core.ExecuteScriptAsync($"try {{ {o.Js}\n}} catch (e) {{ console.error('[Phast site JS]', e); }}");
    }
}
