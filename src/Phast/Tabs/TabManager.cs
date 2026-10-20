using System.Collections.ObjectModel;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Phast.Core;

namespace Phast.Tabs;

/// <summary>A closed tab remembered for Ctrl+Shift+T.</summary>
public sealed record ClosedTab(string Url, string Title, byte[]? FaviconPng, int Index);

/// <summary>
/// Owns the tabs of one window: creation, activation, closing, reordering and "reopen closed tab".
/// WebViews live stacked inside <see cref="Host"/>; only the active one is visible.
/// </summary>
public sealed partial class TabManager : ObservableObject
{
    private const int MaxClosedTabs = 25;
    private readonly Stack<ClosedTab> _closed = new();
    private BrowserTab? _active;

    public TabManager(Panel host)
    {
        Host = host;
        Tabs.CollectionChanged += (_, _) => SyncStrip();
    }

    public Panel Host { get; }
    public ObservableCollection<BrowserTab> Tabs { get; } = new();

    public BrowserTab? Active { get => _active; private set => Set(ref _active, value); }

    public bool HasClosedTabs => _closed.Count > 0;

    /// <summary>Raised when the last tab was closed (the window should close).</summary>
    public event EventHandler? LastTabClosed;
    /// <summary>Raised when a tab wants HTML fullscreen (true) or leaves it (false).</summary>
    public event EventHandler<bool>? FullScreenChanged;
    /// <summary>Raised whenever something worth saving in the session changed.</summary>
    public event EventHandler? SessionDirty;

    public BrowserTab Open(string? url, bool activate = true, int? index = null, string? title = null, byte[]? faviconPng = null)
    {
        Log.Write($"Tab open url={url} activate={activate} index={index}");
        var tab = new BrowserTab(url ?? InternalPages.ToRealUrl(InternalPages.NewTab), title, faviconPng);
        Attach(tab);
        var at = index is int i ? Math.Clamp(i, 0, Tabs.Count) : Tabs.Count;
        Tabs.Insert(at, tab);
        if (activate) Activate(tab);
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);
        return tab;
    }

    /// <summary>Opens a new tab right after the active one (like links opened from a page in Chrome).</summary>
    public BrowserTab OpenNextToActive(string url, bool activate)
    {
        var idx = Active != null ? Tabs.IndexOf(Active) + 1 : Tabs.Count;
        var group = Active?.Group;
        var tab = Open(url, activate, idx);
        if (group != null) SetGroup(tab, group);
        return tab;
    }

    private void Attach(BrowserTab tab)
    {
        tab.NewWindowRequested += OnNewWindowRequested;
        tab.CloseRequested += (_, _) => Close(tab);
        tab.FullScreenChanged += (_, full) => { if (tab == Active) FullScreenChanged?.Invoke(this, full); };
        tab.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(BrowserTab.Url) or nameof(BrowserTab.Title) or nameof(BrowserTab.Group))
                SessionDirty?.Invoke(this, EventArgs.Empty);
        };
    }

    public async void Activate(BrowserTab tab)
    {
        if (tab.Group is { IsCollapsed: true } collapsed) SetCollapsed(collapsed, false);
        if (Active == tab) return;
        Active?.SetActive(false);
        Active = tab;
        tab.SetActive(true);
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);

        // Lazy creation: the WebView only exists once the tab is actually shown.
        if (!tab.HasWebView)
        {
            try { await tab.EnsureWebViewAsync(Host); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"WebView init failed: {ex}"); }
        }
    }

    public void ActivateIndex(int index)
    {
        if (index >= 0 && index < Tabs.Count) Activate(Tabs[index]);
    }

    public void ActivateLast() => ActivateIndex(Tabs.Count - 1);

    public void ActivateRelative(int delta)
    {
        if (Tabs.Count == 0 || Active == null) return;
        var i = (Tabs.IndexOf(Active) + delta + Tabs.Count) % Tabs.Count;
        Activate(Tabs[i]);
    }

    public void Close(BrowserTab tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        if (!string.IsNullOrEmpty(tab.Url) && !InternalPages.IsNewTab(tab.Url))
        {
            _closed.Push(new ClosedTab(tab.Url, tab.Title, tab.FaviconPng, index));
            TrimClosed();
        }

        // Pick the next tab before removing: Chrome selects the tab to the right, else the left one.
        if (tab == Active && Tabs.Count > 1)
            Activate(Tabs[index + 1 < Tabs.Count ? index + 1 : index - 1]);

        Tabs.RemoveAt(index);
        tab.Dispose(Host);
        if (Active == tab) Active = null;
        OnPropertyChanged(nameof(HasClosedTabs));
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);

        if (Tabs.Count == 0) LastTabClosed?.Invoke(this, EventArgs.Empty);
    }

    public void CloseActive()
    {
        if (Active != null) Close(Active);
    }

    public void ReopenClosed()
    {
        if (_closed.Count == 0) return;
        var c = _closed.Pop();
        Open(c.Url, activate: true, index: c.Index, title: c.Title, faviconPng: c.FaviconPng);
        OnPropertyChanged(nameof(HasClosedTabs));
    }

    public void Move(int from, int to)
    {
        if (from == to || from < 0 || to < 0 || from >= Tabs.Count || to >= Tabs.Count) return;
        Tabs.Move(from, to);
        FixGroupAfterMove(Tabs[to]);
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes all tabs and their WebViews (window closing).</summary>
    public void DisposeAll()
    {
        foreach (var t in Tabs) t.Dispose(Host);
    }

    private void TrimClosed()
    {
        if (_closed.Count <= MaxClosedTabs) return;
        var keep = _closed.Take(MaxClosedTabs).Reverse().ToList();
        _closed.Clear();
        foreach (var c in keep) _closed.Push(c);
    }

    /// <summary>Chrome hides the thin separator next to the active tab.</summary>
    private void UpdateSeparators()
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            var next = i + 1 < Tabs.Count ? Tabs[i + 1] : null;
            Tabs[i].ShowSeparator = !Tabs[i].IsActive && next != null && !next.IsActive;
        }
    }

    private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Ctrl+click / middle click: open lazily in the background, no WebView needed yet.
        var background = System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)
                         || System.Windows.Input.Mouse.MiddleButton == System.Windows.Input.MouseButtonState.Pressed;
        if (background && e.IsUserInitiated)
        {
            e.Handled = true;
            OpenNextToActive(e.Uri, activate: false);
            return;
        }

        // Normal popup / target=_blank: hand a real WebView to the page so window.opener keeps working.
        var deferral = e.GetDeferral();
        try
        {
            var tab = new BrowserTab(null);
            Attach(tab);
            var idx = Active != null ? Tabs.IndexOf(Active) + 1 : Tabs.Count;
            tab.Group = Active?.Group;
            Tabs.Insert(idx, tab);
            Activate(tab);
            await tab.EnsureWebViewAsync(Host);
            e.NewWindow = tab.Core;
            e.Handled = true;
            UpdateSeparators();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewWindow failed: {ex}");
        }
        finally
        {
            deferral.Complete();
        }
    }
}
