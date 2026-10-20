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
    private BrowserTab? _observedTab;

    public TabManager Tabs { get; }

    public MainWindow(SessionWindow? restore = null, IEnumerable<string>? urls = null)
    {
        InitializeComponent();

        _frame = new WindowFrame(this, RootGrid, MaximizeButton);
        Tabs = new TabManager(WebHost);
        TabsList.ItemsSource = Tabs.Tabs;

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

            // Restored tabs are lazy: only the active one creates a WebView.
            foreach (var t in restore.Tabs)
            {
                byte[]? icon = null;
                if (t.Favicon != null) { try { icon = Convert.FromBase64String(t.Favicon); } catch { } }
                Tabs.Open(t.Url, activate: false, title: t.Title, faviconPng: icon);
            }
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
        if (_observedTab != null) _observedTab.PropertyChanged -= OnActiveTabPropertyChanged;
        _observedTab = Tabs.Active;
        if (_observedTab != null) _observedTab.PropertyChanged += OnActiveTabPropertyChanged;

        UpdateNavigationButtons();
        UpdateAddressBar(force: true);
        UpdateTitle();

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

    private void FocusPage()
    {
        var wv = Tabs.Active?.WebView;
        if (wv != null) Dispatcher.BeginInvoke(() => wv.Focus(), System.Windows.Threading.DispatcherPriority.Input);
    }

    // ---------------------------------------------------------------- address bar

    private void UpdateAddressBar(bool force = false)
    {
        var url = Tabs.Active?.Url;
        UpdateSiteIcon(url);
        if (AddressBox.IsKeyboardFocusWithin && !force) return; // don't overwrite what the user is typing
        AddressBox.Text = AddressBox.IsKeyboardFocusWithin ? UrlResolver.ToDisplay(url) : ShortDisplay(url);
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

    private void AddressBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressBox.Text = UrlResolver.ToDisplay(Tabs.Active?.Url);
        AddressBox.SelectAll();
    }

    private void AddressBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressBox.Text = ShortDisplay(Tabs.Active?.Url);
        AddressBox.ScrollToHome();
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var url = UrlResolver.Resolve(AddressBox.Text);
            if (url == null) return;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) || Tabs.Active == null)
                Tabs.Open(url);
            else
                Tabs.Active.Navigate(url);
            FocusPage();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            var original = UrlResolver.ToDisplay(Tabs.Active?.Url);
            if (AddressBox.Text != original) { AddressBox.Text = original; AddressBox.SelectAll(); }
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
