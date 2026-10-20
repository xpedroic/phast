using System.Diagnostics;
using Microsoft.Win32;

namespace Phast.Core;

/// <summary>
/// Registers Phast with Windows as a web browser (current user only, no admin rights needed), so it appears in
/// Settings > Apps > Default apps. Windows does not let apps make themselves the default: the user confirms there.
/// </summary>
public static class DefaultBrowser
{
    private const string AppKey = @"Software\Clients\StartMenuInternet\Phast";
    private const string UrlProgId = "PhastURL";
    private const string HtmlProgId = "PhastHTML";

    private static string Exe => Environment.ProcessPath!;
    private static string Command => $"\"{Exe}\" \"%1\"";

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppKey);
        return key != null;
    }

    /// <summary>Is Phast the browser Windows uses for https links?</summary>
    public static bool IsDefault()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
        return key?.GetValue("ProgId") as string == UrlProgId;
    }

    public static void Register()
    {
        using (var app = Registry.CurrentUser.CreateSubKey(AppKey))
        {
            app.SetValue("", "Phast");
            using (var icon = app.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{Exe}\",0");
            using (var open = app.CreateSubKey(@"shell\open\command")) open.SetValue("", $"\"{Exe}\"");
            using var caps = app.CreateSubKey("Capabilities");
            caps.SetValue("ApplicationName", "Phast");
            caps.SetValue("ApplicationDescription", "Phast: a fast, simple and hackable web browser for Windows");
            caps.SetValue("ApplicationIcon", $"\"{Exe}\",0");
            using (var urls = caps.CreateSubKey("URLAssociations"))
                foreach (var scheme in new[] { "http", "https" }) urls.SetValue(scheme, UrlProgId);
            using (var files = caps.CreateSubKey("FileAssociations"))
                foreach (var ext in new[] { ".htm", ".html", ".shtml", ".xhtml", ".svg", ".webp", ".pdf" }) files.SetValue(ext, HtmlProgId);
            using (var menu = caps.CreateSubKey("StartMenu")) menu.SetValue("StartMenuInternet", "Phast");
        }
        using (var registered = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            registered.SetValue("Phast", AppKey + @"\Capabilities");

        RegisterProgId(UrlProgId, "Phast URL", isProtocol: true);
        RegisterProgId(HtmlProgId, "Documento HTML de Phast", isProtocol: false);
    }

    private static void RegisterProgId(string progId, string name, bool isProtocol)
    {
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}");
        key.SetValue("", name);
        if (isProtocol) key.SetValue("URL Protocol", "");
        using (var icon = key.CreateSubKey("DefaultIcon")) icon.SetValue("", $"\"{Exe}\",0");
        using var command = key.CreateSubKey(@"shell\open\command");
        command.SetValue("", Command);
    }

    /// <summary>If Phast was registered from another folder (moved / updated), point Windows to this copy.</summary>
    public static void RefreshIfMoved()
    {
        // Test copies running with their own data folder must never take over the registration.
        if (Environment.GetEnvironmentVariable("PHAST_DATA_DIR") is { Length: > 0 }) return;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{UrlProgId}\shell\open\command");
            if (key?.GetValue("") is string cmd && !string.Equals(cmd, Command, StringComparison.OrdinalIgnoreCase)) Register();
        }
        catch (Exception ex) { Log.Write($"Default browser refresh failed: {ex.Message}"); }
    }

    /// <summary>Opens Windows' default apps page for Phast so the user can confirm.</summary>
    public static void OpenWindowsSettings() =>
        Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=Phast") { UseShellExecute = true });
}
