using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Phast.Bridge;
using Phast.Core;
using Phast.History;

namespace Phast.Tabs;

/// <summary>
/// One browser tab. The WebView2 control is created lazily: a restored or background tab
/// only keeps its URL, title and favicon until the user actually opens it.
/// </summary>
public sealed partial class BrowserTab : ObservableObject
{
    private string _title = "";
    private string _url = "";
    private ImageSource? _favicon;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isActive;
    private bool _showSeparator = true;
    private bool _isAudible;
    private bool _isMuted;

    /// <summary>URL to load once the WebView is created (lazy tabs).</summary>
    private string? _pendingUrl;

    public BrowserTab(string? url, string? title = null, byte[]? faviconPng = null)
    {
        _pendingUrl = url;
        _url = url ?? "";
        _title = title ?? "";
        if (faviconPng != null) SetFaviconFromPng(faviconPng);
    }

    public WebView2? WebView { get; private set; }
    public CoreWebView2? Core => WebView?.CoreWebView2;
    public bool HasWebView => WebView != null;

    /// <summary>Last time this tab was shown (or hidden); used to decide which tabs go to sleep.</summary>
    public DateTime LastActivated { get; private set; } = DateTime.Now;

    /// <summary>PNG bytes of the favicon, kept so sessions can restore it without loading the page.</summary>
    public byte[]? FaviconPng { get; private set; }

    public string Title
    {
        get => _title;
        private set { if (Set(ref _title, value)) OnPropertyChanged(nameof(DisplayTitle)); }
    }

    public string DisplayTitle =>
        !string.IsNullOrWhiteSpace(_title) ? _title
        : InternalPages.IsNewTab(_url) || string.IsNullOrEmpty(_url) ? "Nueva pestaña"
        : Uri.TryCreate(_url, UriKind.Absolute, out var u) && u.Scheme is "http" or "https"
            ? (u.Host.StartsWith("www.") ? u.Host[4..] : u.Host)   // not loaded yet: show the site name
            : UrlResolver.ToDisplay(_url);

    /// <summary>The real URL (internal pages use https://phast.internal/...).</summary>
    public string Url
    {
        get => _url;
        private set { if (Set(ref _url, value)) OnPropertyChanged(nameof(DisplayTitle)); }
    }

    public ImageSource? Favicon { get => _favicon; private set => Set(ref _favicon, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public bool CanGoBack { get => _canGoBack; private set => Set(ref _canGoBack, value); }
    public bool CanGoForward { get => _canGoForward; private set => Set(ref _canGoForward, value); }
    public bool IsActive { get => _isActive; private set => Set(ref _isActive, value); }
    public bool ShowSeparator { get => _showSeparator; set => Set(ref _showSeparator, value); }
    public bool IsAudible { get => _isAudible; private set => Set(ref _isAudible, value); }
    public bool IsMuted { get => _isMuted; private set => Set(ref _isMuted, value); }

    private TabGroup? _group;
    /// <summary>Tab group this tab belongs to (null = ungrouped). Managed by <see cref="TabManager"/>.</summary>
    public TabGroup? Group { get => _group; set => Set(ref _group, value); }

    /// <summary>The page asked to open a popup / new tab.</summary>
    public event EventHandler<CoreWebView2NewWindowRequestedEventArgs>? NewWindowRequested;
    /// <summary>The page called window.close().</summary>
    public event EventHandler? CloseRequested;
    /// <summary>A video/element entered or left HTML fullscreen.</summary>
    public event EventHandler<bool>? FullScreenChanged;

    private Task? _initTask;

    /// <summary>
    /// Creates the WebView2 control inside <paramref name="host"/> using the shared environment.
    /// Concurrent callers share the same initialization. Tabs created without a URL (popups)
    /// do not navigate, so the caller can hand the CoreWebView2 to a NewWindowRequested event.
    /// </summary>
    public Task EnsureWebViewAsync(Panel host) => _initTask ??= InitWebViewAsync(host);

    private async Task InitWebViewAsync(Panel host)
    {
        var webView = new WebView2
        {
            Visibility = IsActive ? Visibility.Visible : Visibility.Hidden,
            DefaultBackgroundColor = System.Drawing.Color.Transparent,
        };
        WebView = webView;
        host.Children.Add(webView);

        var env = await BrowserEnvironment.GetAsync();
        await webView.EnsureCoreWebView2Async(env);
        Log.Write($"WebView ready for {_pendingUrl ?? "(popup)"}");
        if (WebView != webView) return; // tab was closed or discarded while initializing

        var core = webView.CoreWebView2;
        core.SetVirtualHostNameToFolderMapping(InternalPages.Host, AppPaths.Pages, CoreWebView2HostResourceAccessKind.DenyCors);
        core.Settings.IsStatusBarEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.Profile.PreferredColorScheme = Themes.ThemeManager.PreferredScheme;

        core.DocumentTitleChanged += (_, _) =>
        {
            Title = core.DocumentTitle;
            HistoryStore.UpdateTitleAsync(core.Source, core.DocumentTitle).Forget();
        };
        core.SourceChanged += (_, _) => Url = core.Source;
        core.HistoryChanged += (_, _) => { CanGoBack = core.CanGoBack; CanGoForward = core.CanGoForward; };
        core.NavigationStarting += (_, _) => IsLoading = true;
        core.NavigationCompleted += (_, e) =>
        {
            IsLoading = false;
            if (_firstLoad) { _firstLoad = false; Log.Write($"Startup: first page loaded at {Log.SinceStart:F0} ms ({core.Source})"); }
            if (e.IsSuccess) HistoryStore.AddVisitAsync(core.Source, core.DocumentTitle, _typedPending).Forget();
            _typedPending = false;
        };
        core.FaviconChanged += async (_, _) => await UpdateFaviconAsync();
        core.NewWindowRequested += (_, e) => NewWindowRequested?.Invoke(this, e);
        core.WindowCloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        core.ContainsFullScreenElementChanged += (_, _) => FullScreenChanged?.Invoke(this, core.ContainsFullScreenElement);
        core.IsDocumentPlayingAudioChanged += (_, _) => IsAudible = core.IsDocumentPlayingAudio;
        core.IsMutedChanged += (_, _) => IsMuted = core.IsMuted;
        core.WebMessageReceived += OnWebMessageReceived;

        InitSiteScripts(core);
        InitAdblock(core, env);
        InitDarkMode(core);
        InitSiteOverrides(core);
        try { await InitCosmeticAsync(core); }
        catch (Exception ex) { Log.Write($"Cosmetic init failed: {ex.Message}"); }
        if (WebView != webView) return;

        core.MemoryUsageTargetLevel = IsActive ? CoreWebView2MemoryUsageTargetLevel.Normal : CoreWebView2MemoryUsageTargetLevel.Low;

        if (_pendingUrl != null)
        {
            var url = _pendingUrl;
            _pendingUrl = null;
            core.Navigate(url);
        }
    }

    /// <summary>True when the next navigation was typed in the address bar (ranks higher in suggestions).</summary>
    private bool _typedPending;
    private static bool _firstLoad = true;

    public void Navigate(string url, bool typed = false)
    {
        _typedPending = typed;
        Url = url;
        if (Core != null) Core.Navigate(url);
        else _pendingUrl = url;
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        LastActivated = DateTime.Now; // last time the tab was in use (shown or just hidden)
        if (WebView == null) return;

        // Hidden (not Collapsed) keeps layout size, so switching back is instant without a resize.
        WebView.Visibility = active ? Visibility.Visible : Visibility.Hidden;
        if (Core != null)
        {
            // Background tabs ask the engine to trim memory.
            Core.MemoryUsageTargetLevel = active ? CoreWebView2MemoryUsageTargetLevel.Normal : CoreWebView2MemoryUsageTargetLevel.Low;
            if (active && Core.IsSuspended) Core.Resume();
            if (active) IsSuspended = false;
        }
    }

    public void ToggleMute()
    {
        if (Core != null) Core.IsMuted = !Core.IsMuted;
    }

    /// <summary>
    /// Releases the WebView completely but keeps URL, title and favicon, so the tab can be reloaded later.
    /// </summary>
    public void Discard(Panel host)
    {
        if (WebView == null) return;
        _pendingUrl = string.IsNullOrEmpty(_url) ? null : _url;
        var webView = WebView;
        WebView = null;
        _initTask = null;
        host.Children.Remove(webView);
        webView.Dispose();
        IsLoading = false;
        IsAudible = false;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        // Only Phast's own pages may talk to the browser. Web pages can only report class names for element hiding.
        if (!e.Source.StartsWith(InternalPages.Origin + "/", StringComparison.OrdinalIgnoreCase))
        {
            TryHandleCosmeticMessage(e.WebMessageAsJson);
            return;
        }
        PageBridge.Handle(this, e.WebMessageAsJson);
    }

    public void PostMessage(string json) => Core?.PostWebMessageAsJson(json);

    private async Task UpdateFaviconAsync()
    {
        var core = Core;
        if (core == null) return;
        try
        {
            using var stream = await core.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            if (stream == null || stream.Length == 0) { Favicon = null; FaviconPng = null; return; }
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var png = ms.ToArray();
            SetFaviconFromPng(png);
            FaviconCache.Save(core.Source, png);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Favicon error: {ex.Message}");
        }
    }

    private void SetFaviconFromPng(byte[] png)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(png);
            img.EndInit();
            img.Freeze();
            Favicon = img;
            FaviconPng = png;
        }
        catch
        {
            Favicon = null;
            FaviconPng = null;
        }
    }

    public void Dispose(Panel host)
    {
        if (WebView == null) return;
        var webView = WebView;
        WebView = null;
        host.Children.Remove(webView);
        webView.Dispose();
    }
}
