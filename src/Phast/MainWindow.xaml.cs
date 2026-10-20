using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using Phast.Core;
using Phast.Interop;
using Phast.Session;
using Phast.Tabs;
using Phast.Themes;

namespace Phast;

/// <summary>
/// A browser window: Chrome-like title bar with tabs, toolbar with the address bar, and the web content area.
/// Split in partial classes: Shortcuts, TabDrag, Menu, FullScreen.
/// </summary>
public partial class MainWindow : Window
{
    private readonly WindowFrame _frame;
    private readonly Omnibox.OmniboxController _omnibox;
    private BrowserTab? _observedTab;

    public TabManager Tabs { get; }

    public MainWindow(SessionWindow? restore = null, IEnumerable<string>? urls = null)
    {
        InitializeComponent();
        Log.Write($"Startup:   XAML loaded at {Log.SinceStart:F0} ms");

        _frame = new WindowFrame(this, RootGrid, MaximizeButton);
        Tabs = new TabManager(WebHost);
        TabsList.ItemsSource = Tabs.StripItems;

        Tabs.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TabManager.Active)) OnActiveTabChanged(); };
        Tabs.LastTabClosed += (_, _) => Close();
        Tabs.FullScreenChanged += (_, full) => SetFullScreen(full);
        Tabs.SessionDirty += (_, _) => SessionManager.MarkDirty();

        ThemeManager.ThemeChanged += OnThemeChanged;
        SourceInitialized += (_, _) => _frame.SetDarkFrame(ThemeManager.IsDark);
        StateChanged += (_, _) => OnWindowStateChanged();
        TitleBar.SizeChanged += (_, _) => UpdateTabAreaWidth();
        CaptionButtons.SizeChanged += (_, _) => UpdateTabAreaWidth();
        PreviewKeyDown += OnPreviewKeyDown;
        MouseDown += OnMouseDown;
        Activated += (_, _) => SessionManager.LastActiveWindow = this;
        InitTabDrag();
        InitBookmarks();
        InitAdblockUi();
        InitDarkModeUi();
        InitDownloadsUi();
        InitExtrasUi();

        _omnibox = new Omnibox.OmniboxController(AddressBox, SuggestPopup, SuggestList, NavigateFromOmnibox);
        AddressBox.PreviewTextInput += (_, _) => _lastAddressTyping = DateTime.Now;
        AddressBox.PreviewKeyDown += (_, e) =>
        {
            // Keys that deliberately leave the address bar don't count as "typing".
            if (e.Key is not (Key.Enter or Key.Escape or Key.Tab)) _lastAddressTyping = DateTime.Now;
            else _lastAddressTyping = DateTime.MinValue;
        };
        // A Popup does not follow its window: close the dropdown whenever the window moves or loses focus.
        Deactivated += (_, _) => _omnibox.Close();
        LocationChanged += (_, _) => _omnibox.Close();
        SizeChanged += (_, _) => _omnibox.Close();

        Log.Write($"Startup:   window parts ready at {Log.SinceStart:F0} ms");
        RestoreTabs(restore, urls);
        OnWindowStateChanged();
    }

    // ---------------------------------------------------------------- session

    private void RestoreTabs(SessionWindow? restore, IEnumerable<string>? urls)
    {
        if (restore != null)
        {
            if (!double.IsNaN(restore.Left) && !double.IsNaN(restore.Top) && IsOnScreen(restore))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = restore.Left;
                Top = restore.Top;
            }
            Width = Math.Max(MinWidth, restore.Width);
            Height = Math.Max(MinHeight, restore.Height);
            if (restore.Maximized) WindowState = WindowState.Maximized;

            var groups = restore.Groups.ToDictionary(g => g.Id, g =>
                new TabGroup(g.Name, g.Color, g.PageGroupId) { Id = g.Id, IsCollapsed = g.Collapsed });

            // Restored tabs are lazy: only the active one creates a WebView.
            foreach (var t in restore.Tabs)
            {
                byte[]? icon = null;
                if (t.Favicon != null) { try { icon = Convert.FromBase64String(t.Favicon); } catch { } }
                var tab = Tabs.Open(t.Url, activate: false, title: t.Title, faviconPng: icon);
                if (t.GroupId != null && groups.TryGetValue(t.GroupId, out var group)) tab.Group = group;
            }
            Tabs.SyncStrip();
            if (Tabs.Tabs.Count > 0)
                Tabs.ActivateIndex(Math.Clamp(restore.ActiveIndex, 0, Tabs.Tabs.Count - 1));
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        if (urls != null)
            foreach (var url in urls) Tabs.Open(url, activate: true);

        if (Tabs.Tabs.Count == 0)
            Tabs.Open(null);
    }

    private static bool IsOnScreen(SessionWindow w) =>
        w.Left + 100 > SystemParameters.VirtualScreenLeft &&
        w.Top >= SystemParameters.VirtualScreenTop - 10 &&
        w.Left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100 &&
        w.Top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 50;

    /// <summary>Snapshot of this window for session.json.</summary>
    public SessionWindow ToSession()
    {
        var bounds = WindowState == WindowState.Normal && !_isFullScreen ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (_isFullScreen) bounds = _boundsBeforeFullScreen;
        var tabs = Tabs.Tabs.Where(t => !string.IsNullOrEmpty(t.Url)).ToList();
        return new SessionWindow
        {
            Tabs = tabs.Select(t => new SessionTab
            {
                Url = t.Url,
                Title = t.Title,
                Favicon = t.FaviconPng != null && t.FaviconPng.Length < 16_000 ? Convert.ToBase64String(t.FaviconPng) : null,
                GroupId = t.Group?.Id,
            }).ToList(),
            Groups = tabs.Select(t => t.Group).OfType<TabGroup>().Distinct().Select(g => new SessionGroup
            {
                Id = g.Id, Name = g.Name, Color = g.Color, Collapsed = g.IsCollapsed, PageGroupId = g.PageGroupId,
            }).ToList(),
            ActiveIndex = Tabs.Active != null ? Math.Max(0, tabs.IndexOf(Tabs.Active)) : 0,
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = WindowState == WindowState.Maximized || (_isFullScreen && _stateBeforeFullScreen == WindowState.Maximized),
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        Tabs.DisposeAll();
        base.OnClosed(e);
    }

    // ---------------------------------------------------------------- active tab

    private void OnActiveTabChanged()
    {
        if (_observedTab != null) { _observedTab.PropertyChanged -= OnActiveTabPropertyChanged; _observedTab.ZoomChanged -= OnActiveZoomChanged; }
        _observedTab = Tabs.Active;
        if (_observedTab != null) { _observedTab.PropertyChanged += OnActiveTabPropertyChanged; _observedTab.ZoomChanged += OnActiveZoomChanged; }
        UpdateZoomButton();

        UpdateNavigationButtons();
        UpdateAddressBar(force: true);
        UpdateTitle();
        UpdateShield();
        UpdateDarkButton();

        // On a new tab page the cursor goes straight to the address bar, like Chrome.
        if (_observedTab != null && InternalPages.IsNewTab(_observedTab.Url))
            Dispatcher.BeginInvoke(() => FocusAddressBar(), System.Windows.Threading.DispatcherPriority.Input);
        else
            FocusPage();
    }

    private void OnActiveTabPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(BrowserTab.Url):
                UpdateAddressBar();
                break;
            case nameof(BrowserTab.DisplayTitle):
                UpdateTitle();
                break;
            case nameof(BrowserTab.CanGoBack):
            case nameof(BrowserTab.CanGoForward):
            case nameof(BrowserTab.IsLoading):
                UpdateNavigationButtons();
                break;
            case nameof(BrowserTab.BlockedCount):
            case nameof(BrowserTab.BlockingActive):
                UpdateShield();
                break;
            case nameof(BrowserTab.IsDarkForced):
            case nameof(BrowserTab.DocumentHost):
                UpdateDarkButton();
                break;
        }
    }

    private void UpdateTitle() => Title = Tabs.Active != null ? $"{Tabs.Active.DisplayTitle} - Phast" : "Phast";

    private void UpdateNavigationButtons()
    {
        var tab = Tabs.Active;
        BackButton.IsEnabled = tab?.CanGoBack == true;
        ForwardButton.IsEnabled = tab?.CanGoForward == true;
        var loading = tab?.IsLoading == true;
        ReloadButton.Content = loading ? "" : "";
        ReloadButton.ToolTip = loading ? "Detener la carga de esta página" : "Recargar esta página (F5)";
    }

    /// <summary>True while Phast itself is moving focus to the page (so the address bar lets it go).</summary>
    private bool _pageFocusRequested;
    /// <summary>Last time the user typed in the address bar.</summary>
    private DateTime _lastAddressTyping;

    private void FocusPage()
    {
        var wv = Tabs.Active?.WebView;
        if (wv == null) return;
        _pageFocusRequested = true;
        Dispatcher.BeginInvoke(() =>
        {
            wv.Focus();
            _pageFocusRequested = false;
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    // ---------------------------------------------------------------- address bar

    private void UpdateAddressBar(bool force = false)
    {
        var url = Tabs.Active?.Url;
        UpdateSiteIcon(url);
        UpdateStar();
        // Never overwrite what the user is typing (Chrome does the same); untouched text follows the page.
        if (_omnibox.UserEdited && !force) return;
        _addressLostToOtherWindow = false;
        var focused = AddressBox.IsKeyboardFocusWithin;
        _omnibox.SetText(focused ? UrlResolver.ToDisplay(url) : ShortDisplay(url));
        if (focused) AddressBox.SelectAll();
    }

    /// <summary>Chrome hides "https://" and "www." while the address bar is not focused.</summary>
    private static string ShortDisplay(string? url)
    {
        var text = UrlResolver.ToDisplay(url);
        if (text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) text = text[8..];
        if (text.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) text = text[4..];
        if (text.EndsWith('/') && text.IndexOf('/') == text.Length - 1) text = text[..^1];
        return text;
    }

    private void UpdateSiteIcon(string? url)
    {
        if (url == null || InternalPages.IsInternal(url) || url.Length == 0)
        {
            SiteInfoButton.Content = ""; // search
            SiteInfoButton.ToolTip = null;
        }
        else if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            SiteInfoButton.Content = ""; // lock
            SiteInfoButton.ToolTip = "La conexión es segura";
        }
        else
        {
            SiteInfoButton.Content = ""; // info
            SiteInfoButton.ToolTip = "No es seguro";
        }
    }

    public void FocusAddressBar()
    {
        AddressBox.Focus();
        AddressBox.SelectAll();
    }

    private void AddressBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // First click selects everything (like Chrome); further clicks place the caret.
        if (!AddressBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            FocusAddressBar();
        }
    }

    /// <summary>The address bar lost focus because the whole window was deactivated (Alt+Tab...).</summary>
    private bool _addressLostToOtherWindow;

    private void AddressBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Coming back from another window: keep whatever the user was typing, like Chrome.
        if (_addressLostToOtherWindow)
        {
            _addressLostToOtherWindow = false;
            return;
        }
        _omnibox.SetText(UrlResolver.ToDisplay(Tabs.Active?.Url));
        AddressBox.SelectAll();
    }

    private void AddressBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Log.Write($"Address lost focus -> {e.NewFocus?.GetType().Name ?? "null"} active={IsActive}");

        // A page that finishes loading (or calls focus()) can steal focus from WebView2's host while the
        // user is typing here. If the user did not click anywhere and was typing a moment ago, take it back.
        var typingRecently = (DateTime.Now - _lastAddressTyping).TotalSeconds < 3;
        if (IsActive && typingRecently && !_pageFocusRequested && !NativeMethods.AnyMouseButtonDown()
            && (e.NewFocus == null || e.NewFocus is Microsoft.Web.WebView2.Wpf.WebView2))
        {
            _addressLostToOtherWindow = true; // keep the typed text
            Dispatcher.BeginInvoke(() =>
            {
                AddressBox.Focus();
                AddressBox.CaretIndex = AddressBox.Text.Length;
            }, System.Windows.Threading.DispatcherPriority.Input);
            return;
        }

        if (e.NewFocus == null || !IsActive)
        {
            _addressLostToOtherWindow = true;
            return;
        }
        _addressLostToOtherWindow = false;
        _omnibox.SetText(ShortDisplay(Tabs.Active?.Url));
        AddressBox.ScrollToHome();
    }

    /// <summary>Called by the omnibox when the user picks an address, search or suggestion.</summary>
    private void NavigateFromOmnibox(string url, bool newTab)
    {
        Log.Write($"NavigateFromOmnibox {url} newTab={newTab} active={Tabs.Active?.Url}");
        if (newTab || Tabs.Active == null) Tabs.Open(url);
        else Tabs.Active.Navigate(url, typed: true);
        FocusPage();
    }

    private void Suggestion_MouseEnter(object sender, MouseEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Omnibox.Suggestion s) _omnibox.Highlight(s);
    }

    private void Suggestion_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is Omnibox.Suggestion s)
            _omnibox.OpenSuggestion(s, newTab: Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
        e.Handled = true;
    }

    private void Suggestion_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && (sender as FrameworkElement)?.DataContext is Omnibox.Suggestion s)
        {
            _omnibox.OpenSuggestion(s, newTab: true);
            e.Handled = true;
        }
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            var original = UrlResolver.ToDisplay(Tabs.Active?.Url);
            if (AddressBox.Text != original) { _omnibox.SetText(original); AddressBox.SelectAll(); }
            else FocusPage();
        }
    }

    // ---------------------------------------------------------------- toolbar

    private void Back_Click(object sender, RoutedEventArgs e) => Execute(Input.Commands.Back);
    private void Forward_Click(object sender, RoutedEventArgs e) => Execute(Input.Commands.Forward);

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (Tabs.Active?.IsLoading == true) Tabs.Active.Core?.Stop();
        else Execute(Input.Commands.Reload);
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => Execute(Input.Commands.NewTab);

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.XButton1) { Execute(Input.Commands.Back); e.Handled = true; }
        else if (e.ChangedButton == MouseButton.XButton2) { Execute(Input.Commands.Forward); e.Handled = true; }
    }

    // ---------------------------------------------------------------- window chrome

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseWindow_Click(object sender, RoutedEventArgs e) => Close();

    private void OnWindowStateChanged()
    {
        // Minimized: let the engine treat the page as hidden (throttled, memory trimmed) and shrink our own process.
        Tabs.Active?.SetMinimized(WindowState == WindowState.Minimized);
        if (WindowState == WindowState.Minimized) Interop.MemoryTrim.TrimSoon();

        var max = WindowState == WindowState.Maximized;
        MaximizeButton.Content = max ? "" : "";
        MaximizeButton.ToolTip = max ? "Restaurar" : "Maximizar";
        // Chrome leaves a small gap above the tabs when the window is not maximized.
        TitleBar.Height = max ? 34 : 41;
        System.Windows.Shell.WindowChrome.GetWindowChrome(this).CaptionHeight = TitleBar.Height;
    }

    /// <summary>Tabs can use the whole strip except the caption buttons, the "+" button and a small drag area.</summary>
    private void UpdateTabAreaWidth()
    {
        const double minDragArea = 48;
        var available = TitleBar.ActualWidth - CaptionButtons.ActualWidth - NewTabButton.ActualWidth
                        - NewTabButton.Margin.Left - TabArea.Margin.Left - minDragArea;
        TabsList.MaxWidth = Math.Max(0, available);
    }

    private void OnThemeChanged()
    {
        _frame.SetDarkFrame(ThemeManager.IsDark);
        // All tabs share one profile, so updating any loaded tab updates every page.
        var core = Tabs.Tabs.FirstOrDefault(t => t.Core != null)?.Core;
        if (core != null) core.Profile.PreferredColorScheme = ThemeManager.PreferredScheme;
    }
}
