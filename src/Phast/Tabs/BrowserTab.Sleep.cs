using Microsoft.Web.WebView2.Core;
using System.Windows.Controls;

namespace Phast.Tabs;

/// <summary>Sleeping tabs: suspended (frozen by the engine) or discarded (WebView released, URL and title kept).</summary>
public sealed partial class BrowserTab
{
    private bool _isSuspended;

    /// <summary>Stable id of the tab during this run (used by the developer tools).</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The engine froze this tab to save CPU and memory; it wakes up when shown.</summary>
    public bool IsSuspended { get => _isSuspended; private set => Set(ref _isSuspended, value); }

    /// <summary>Human-readable state for the tab monitor.</summary>
    public string StateLabel =>
        IsActive ? "Activa" : !HasWebView ? "Dormida" : IsSuspended ? "Suspendida" : "En segundo plano";

    /// <summary>The window was minimized/restored: hide the page from the engine while nobody can see it.</summary>
    public void SetMinimized(bool minimized)
    {
        if (WebView == null || !IsActive) return;
        WebView.Visibility = minimized ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;
        if (Core != null)
            Core.MemoryUsageTargetLevel = minimized ? CoreWebView2MemoryUsageTargetLevel.Low : CoreWebView2MemoryUsageTargetLevel.Normal;
    }

    /// <summary>Asks the engine to freeze this background tab. Returns false if it refused (e.g. audio playing).</summary>
    public async Task<bool> TrySuspendAsync()
    {
        if (Core == null || IsActive || IsSuspended || IsAudible) return false;
        try
        {
            IsSuspended = await Core.TrySuspendAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            IsSuspended = false;
        }
        return IsSuspended;
    }

    /// <summary>Releases the WebView of a background tab (it reloads when opened again).</summary>
    public bool Sleep(Panel host)
    {
        if (IsActive || !HasWebView || IsAudible) return false;
        Discard(host);
        IsSuspended = false;
        OnPropertyChanged(nameof(HasWebView));
        return true;
    }
}
