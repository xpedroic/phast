using Microsoft.Web.WebView2.Core;
using Phast.Core;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>Passwords/autofill settings, per-site zoom and recovery from crashed pages.</summary>
public sealed partial class BrowserTab
{
    private bool _applyingZoom;

    /// <summary>Applies the "save passwords" and "autofill" settings to this tab's engine.</summary>
    public void ApplyFormSettings()
    {
        if (Core == null) return;
        Core.Settings.IsPasswordAutosaveEnabled = SettingsService.Current.SavePasswords;
        Core.Settings.IsGeneralAutofillEnabled = SettingsService.Current.Autofill;
    }

    // ---------------------------------------------------------------- per-site zoom (like Chrome)

    /// <summary>Raised when the zoom of the current page changes (the address bar shows a zoom indicator).</summary>
    public event EventHandler? ZoomChanged;

    public double Zoom => WebView?.ZoomFactor ?? 1;

    /// <summary>
    /// Zoom changed. The engine reports user zoom (Ctrl+wheel, Ctrl+0...) by itself; zoom set from code
    /// (the menu's +/- buttons) must call this explicitly.
    /// </summary>
    public void OnZoomChanged()
    {
        if (!_applyingZoom) ZoomMemory.Set(DocumentHost, Zoom);
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies the remembered zoom of the site being shown (called when a page commits).</summary>
    private void ApplySiteZoom()
    {
        if (WebView == null) return;
        var zoom = ZoomMemory.Get(DocumentHost);
        if (Math.Abs(WebView.ZoomFactor - zoom) < 0.001) return;
        _applyingZoom = true;
        WebView.ZoomFactor = zoom;
        _applyingZoom = false;
    }

    public void ResetZoom()
    {
        if (WebView == null) return;
        WebView.ZoomFactor = 1;
        OnZoomChanged();
    }

    // ---------------------------------------------------------------- crashed pages

    private DateTime _lastAutoReload;

    private void OnProcessFailed(object? sender, CoreWebView2ProcessFailedEventArgs e)
    {
        Log.Write($"Process failed: {e.ProcessFailedKind} ({e.Reason}) in {Url}");
        switch (e.ProcessFailedKind)
        {
            case CoreWebView2ProcessFailedKind.RenderProcessExited:
            case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive when e.Reason != CoreWebView2ProcessFailedReason.Unresponsive:
            case CoreWebView2ProcessFailedKind.FrameRenderProcessExited:
                // The page's process died: reload it once (avoid loops if the page keeps crashing).
                if (DateTime.Now - _lastAutoReload > TimeSpan.FromSeconds(30) && IsActive)
                {
                    _lastAutoReload = DateTime.Now;
                    Core?.Reload();
                }
                break;
            case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                // The whole engine went away: every tab is dead. Restart Phast; the session brings the tabs back.
                CrashGuard.EngineLost();
                break;
        }
    }
}
