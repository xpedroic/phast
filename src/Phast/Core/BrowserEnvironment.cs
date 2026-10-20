using Microsoft.Web.WebView2.Core;

namespace Phast.Core;

/// <summary>
/// The single WebView2 environment shared by every tab and window.
/// Sharing it means one browser process for the whole app, which saves a lot of memory.
/// Creation starts as early as possible at startup (in parallel with building the window).
/// </summary>
public static class BrowserEnvironment
{
    private static Task<CoreWebView2Environment>? _task;

    /// <summary>Kicks off environment creation. Safe to call several times.</summary>
    public static Task<CoreWebView2Environment> GetAsync()
    {
        return _task ??= CreateAsync();
    }

    private static async Task<CoreWebView2Environment> CreateAsync()
    {
        var options = new CoreWebView2EnvironmentOptions
        {
            // Extra flags will become configurable in Settings > Advanced.
            AdditionalBrowserArguments = "",
        };
        return await CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, AppPaths.UserData, options);
    }

    /// <summary>Installed WebView2 runtime version, or null if the runtime is missing.</summary>
    public static string? RuntimeVersion()
    {
        try { return CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch (WebView2RuntimeNotFoundException) { return null; }
    }
}
