using Microsoft.Web.WebView2.Core;
using Phast.Adblock;
using Phast.Core;

namespace Phast.Tabs;

/// <summary>
/// Site scripts (Twitch / YouTube ad blockers) for one tab. They must exist before the page's first script runs,
/// so the first navigation to such a site is paused, the script is registered, and the navigation restarts.
/// </summary>
public sealed partial class BrowserTab
{
    /// <summary>Site script id → id returned by AddScriptToExecuteOnDocumentCreatedAsync.</summary>
    private readonly Dictionary<string, string> _siteScripts = new();

    private void InitSiteScripts(CoreWebView2 core)
    {
        _siteScripts.Clear();
        core.NavigationStarting += OnNavigationStartingSiteScripts;
    }

    private void OnNavigationStartingSiteScripts(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) return;
        var missing = SiteScripts.For(uri.Host.ToLowerInvariant()).Where(s => !_siteScripts.ContainsKey(s.Id)).ToList();
        if (missing.Count == 0) return;

        e.Cancel = true;
        _ = RegisterThenNavigateAsync(missing, e.Uri);
    }

    private async Task RegisterThenNavigateAsync(List<SiteScript> scripts, string url)
    {
        var core = Core;
        if (core == null) return;
        foreach (var script in scripts)
        {
            try
            {
                _siteScripts[script.Id] = "";
                _siteScripts[script.Id] = await core.AddScriptToExecuteOnDocumentCreatedAsync(script.LoadSource());
                Log.Write($"Site script '{script.Id}' registered");
            }
            catch (Exception ex)
            {
                Log.Write($"Site script '{script.Id}' failed: {ex.Message}");
            }
        }
        if (Core == core) core.Navigate(url);
    }

    /// <summary>Unregisters scripts that were turned off in Settings (takes effect on the next page load).</summary>
    public void RefreshSiteScripts()
    {
        if (Core == null) return;
        foreach (var script in SiteScripts.All.Where(s => !s.Enabled() && _siteScripts.ContainsKey(s.Id)).ToList())
        {
            if (_siteScripts[script.Id].Length > 0) Core.RemoveScriptToExecuteOnDocumentCreated(_siteScripts[script.Id]);
            _siteScripts.Remove(script.Id);
        }
    }
}
