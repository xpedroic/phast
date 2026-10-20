using System.IO;
using Microsoft.Web.WebView2.Core;
using Phast.Core;
using Phast.Dark;

namespace Phast.Tabs;

/// <summary>Instant dark mode (Dark Reader) for one tab.</summary>
public sealed partial class BrowserTab
{
    private string? _darkScriptId;
    private bool _isDarkForced;

    /// <summary>Is the current page shown with forced dark mode?</summary>
    public bool IsDarkForced { get => _isDarkForced; private set => Set(ref _isDarkForced, value); }

    private void InitDarkMode(CoreWebView2 core)
    {
        _darkScriptId = null;
        core.AddWebResourceRequestedFilter($"https://{DarkModeService.CssProxyHost}/*", CoreWebView2WebResourceContext.All,
            CoreWebView2WebResourceRequestSourceKinds.All);
        core.NavigationStarting += (_, e) =>
            IsDarkForced = Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && DarkModeService.IsWanted(u.Host);
    }

    /// <summary>The page about to load needs the dark script and this tab hasn't registered it yet.</summary>
    private bool NeedsDarkRegistration(string host) => _darkScriptId == null && DarkModeService.IsWanted(host);

    private async Task RegisterDarkScriptAsync()
    {
        var core = Core;
        if (core == null) return;
        if (!string.IsNullOrEmpty(_darkScriptId)) core.RemoveScriptToExecuteOnDocumentCreated(_darkScriptId);
        _darkScriptId = "";
        _darkScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(DarkModeService.DocumentStartScript());
    }

    /// <summary>
    /// Dark mode choices changed (here or in another tab): update the current page instantly and keep the
    /// document-start script in sync for the next navigations.
    /// </summary>
    public async Task RefreshDarkModeAsync()
    {
        var core = Core;
        if (core == null) return;
        var wanted = DarkModeService.IsWanted(DocumentHost);
        if (wanted != IsDarkForced)
        {
            IsDarkForced = wanted;
            await core.ExecuteScriptAsync(DarkModeService.InstantScript(wanted));
        }
        if (_darkScriptId != null || wanted) await RegisterDarkScriptAsync();
    }

    /// <summary>Serves Dark Reader's requests for cross-origin stylesheets/images. Returns true if it was one.</summary>
    private bool TryHandleCssProxy(CoreWebView2WebResourceRequestedEventArgs e)
    {
        var url = e.Request.Uri;
        if (!url.StartsWith($"https://{DarkModeService.CssProxyHost}/", StringComparison.Ordinal)) return false;

        var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        if (!IsDarkForced || _env == null || !Uri.TryCreate(query["u"], UriKind.Absolute, out var target))
        {
            if (_env != null) e.Response = _env.CreateWebResourceResponse(null, 403, "Forbidden", "");
            return true;
        }

        var deferral = e.GetDeferral();
        ServeAsync();
        return true;

        async void ServeAsync()
        {
            try
            {
                var result = await CssProxy.FetchAsync(target);
                e.Response = result is { } r
                    ? _env.CreateWebResourceResponse(new MemoryStream(r.Data), 200, "OK",
                        $"Content-Type: {r.ContentType}\r\nAccess-Control-Allow-Origin: *")
                    : _env.CreateWebResourceResponse(null, 404, "Not Found", "Access-Control-Allow-Origin: *");
            }
            catch (Exception ex)
            {
                Log.Write($"CSS proxy failed for {target}: {ex.Message}");
            }
            finally
            {
                deferral.Complete();
            }
        }
    }
}
