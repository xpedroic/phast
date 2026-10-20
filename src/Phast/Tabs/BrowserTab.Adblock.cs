using Microsoft.Web.WebView2.Core;
using Phast.Adblock;
using Phast.Core;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>Ad blocking for one tab: checks every request against the filter engine and counts what was blocked.</summary>
public sealed partial class BrowserTab
{
    private const int MaxBlockedLog = 200;

    private CoreWebView2Environment? _env;
    private bool _requestFilterAdded;
    private string _mainUrl = "";
    private string _docHost = "";
    private bool _blockingActive;
    private int _blockedCount;
    private readonly Queue<string> _blockedLog = new();
    private int _checkedRequests;
    private long _matchTicks;

    /// <summary>Requests blocked on the current page.</summary>
    public int BlockedCount { get => _blockedCount; private set => Set(ref _blockedCount, value); }

    /// <summary>Host of the page currently shown (e.g. "www.marca.com").</summary>
    public string DocumentHost => _docHost;

    /// <summary>Is the blocker working on the current page (on globally and not disabled for this site)?</summary>
    public bool BlockingActive => _blockingActive;

    /// <summary>Last blocked URLs on this page (newest last), for the developer tools in Settings.</summary>
    public IReadOnlyCollection<string> BlockedRequests => _blockedLog;

    private void InitAdblock(CoreWebView2 core, CoreWebView2Environment env)
    {
        _env = env;
        _requestFilterAdded = false; // new WebView (first load, or reloaded after sleeping)
        EnsureRequestFilter();
        core.NavigationStarting += (_, e) => OnMainNavigationStarting(e.Uri, e.IsRedirected);
        core.WebResourceRequested += OnWebResourceRequested;
    }

    /// <summary>
    /// Asks WebView2 to route requests through <see cref="OnWebResourceRequested"/>. Only done while ad blocking
    /// is enabled, because every routed request costs a trip to the UI thread.
    /// </summary>
    public void EnsureRequestFilter()
    {
        if (Core == null || _requestFilterAdded || !SettingsService.Current.AdblockEnabled) return;
        Core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        _requestFilterAdded = true;
    }

    private void OnMainNavigationStarting(string uri, bool redirected)
    {
        _mainUrl = uri;
        _docHost = Uri.TryCreate(uri, UriKind.Absolute, out var u) ? u.Host.ToLowerInvariant() : "";
        // Never on Phast's own pages (nothing to block there, and late requests of the previous page would be counted).
        _blockingActive = (u?.Scheme is "http" or "https") && _docHost != InternalPages.Host && AdblockService.IsActiveFor(_docHost);
        if (!redirected)
        {
            if (Log.Enabled && _checkedRequests > 0)
                Log.Write($"Adblock stats: {_checkedRequests} requests checked, {BlockedCount} blocked, " +
                          $"{System.Diagnostics.Stopwatch.GetElapsedTime(0, _matchTicks).TotalMilliseconds:F2} ms total matching");
            BlockedCount = 0;
            _blockedLog.Clear();
            _checkedRequests = 0;
            _matchTicks = 0;
        }
        OnPropertyChanged(nameof(DocumentHost));
        OnPropertyChanged(nameof(BlockingActive));
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // This runs for every request of the page: keep it as cheap as possible.
        if (TryHandleCssProxy(e)) return;
        if (!_blockingActive || _env == null) return;
        var engine = AdblockService.Engine;
        if (engine == null) return;

        var uri = e.Request.Uri;
        var context = e.ResourceContext;
        if (context == CoreWebView2WebResourceContext.Document && uri == _mainUrl) return; // never block the page itself

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var rule = engine.Match(uri, _docHost, MapType(context));
        _checkedRequests++;
        _matchTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started;
        if (rule == null) return;

        e.Response = _env.CreateWebResourceResponse(null, 403, "Blocked by Phast", "");
        if (Log.Enabled) Log.Write($"Blocked [{context}] {uri}");
        BlockedCount++;
        _blockedLog.Enqueue(uri);
        if (_blockedLog.Count > MaxBlockedLog) _blockedLog.Dequeue();
    }

    private static ResourceType MapType(CoreWebView2WebResourceContext context) => context switch
    {
        CoreWebView2WebResourceContext.Document => ResourceType.Subdocument,
        CoreWebView2WebResourceContext.Stylesheet => ResourceType.Stylesheet,
        CoreWebView2WebResourceContext.Image => ResourceType.Image,
        CoreWebView2WebResourceContext.Media => ResourceType.Media,
        CoreWebView2WebResourceContext.Font => ResourceType.Font,
        CoreWebView2WebResourceContext.Script => ResourceType.Script,
        CoreWebView2WebResourceContext.XmlHttpRequest or CoreWebView2WebResourceContext.Fetch => ResourceType.Xhr,
        CoreWebView2WebResourceContext.Websocket => ResourceType.WebSocket,
        CoreWebView2WebResourceContext.Ping => ResourceType.Ping,
        _ => ResourceType.Other,
    };
}
