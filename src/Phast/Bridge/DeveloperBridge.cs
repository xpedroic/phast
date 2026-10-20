using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Threading;
using Phast.Core;
using Phast.Developer;
using Phast.Input;
using Phast.Session;
using Phast.Settings;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>Messages of the "Advanced / developer mode" section of phast://settings.</summary>
public static class DeveloperBridge
{
    private static DispatcherTimer? _monitorTimer;
    private static BrowserTab? _monitorPage;

    public static void Handle(BrowserTab page, string type, JsonElement msg)
    {
        string S(string name) => msg.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

        switch (type)
        {
            case "dev.info":
                page.PostMessage(new JsonObject
                {
                    ["type"] = "devInfo",
                    ["restartNeeded"] = BrowserEnvironment.StartedWith != null && BrowserEnvironment.StartedWith != BrowserEnvironment.BrowserArguments(),
                    ["startedWith"] = BrowserEnvironment.StartedWith ?? "",
                    ["dataFolder"] = AppPaths.Root,
                }.ToJsonString());
                break;
            case "dev.restart":
                AppRestart.Restart();
                break;
            case "dev.openData":
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.Root}\"") { UseShellExecute = true });
                break;
            case "dev.export":
                Export(page);
                break;
            case "dev.import":
                Import(page);
                break;

            // ---- tab monitor
            case "monitor.start":
                StartMonitor(page);
                break;
            case "monitor.stop":
                StopMonitor();
                break;
            case "tab.sleep" or "tab.close" or "tab.activate" or "tab.devtools" or "tab.blocked":
                TabAction(page, type, S("id"));
                break;

            // ---- keyboard shortcuts
            case "shortcuts.get":
                page.PostMessage(ShortcutsMessage());
                break;
            case "shortcuts.capture":
                ShortcutCapture.Start(page, S("command"));
                break;
            case "shortcuts.set":
                var command = S("command");
                if (Commands.All.Any(c => c.Id == command) && (S("shortcut").Length == 0 || Shortcut.Parse(S("shortcut")) != null))
                    SettingsService.Update(s => s.Shortcuts[command] = S("shortcut"));
                page.PostMessage(ShortcutsMessage());
                break;
            case "shortcuts.reset":
                var id = S("command");
                SettingsService.Update(s => { if (id.Length == 0) s.Shortcuts.Clear(); else s.Shortcuts.Remove(id); });
                page.PostMessage(ShortcutsMessage());
                break;

            // ---- per-site tweaks
            case "site.set":
                var site = S("site").Trim().ToLowerInvariant();
                if (site.Length == 0) break;
                var value = msg.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Object
                    ? v.Deserialize<SiteOverride>(JsonFile.Options) : null;
                SettingsService.Update(s =>
                {
                    if (value == null) s.SiteOverrides.Remove(site);
                    else s.SiteOverrides[site] = value;
                });
                break;
        }
    }

    // ---------------------------------------------------------------- shortcuts

    public static string ShortcutsMessage() => new JsonObject
    {
        ["type"] = "shortcuts",
        ["items"] = new JsonArray(Commands.All.Select(c => (JsonNode)new JsonObject
        {
            ["id"] = c.Id,
            ["name"] = c.Name,
            ["shortcuts"] = new JsonArray(App.Shortcuts.ShortcutsOf(c.Id).Select(x => (JsonNode)x!).ToArray()),
            ["custom"] = SettingsService.Current.Shortcuts.ContainsKey(c.Id),
        }).ToArray()),
    }.ToJsonString();

    // ---------------------------------------------------------------- monitor

    private static void StartMonitor(BrowserTab page)
    {
        _monitorPage = page;
        if (_monitorTimer == null)
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _monitorTimer.Tick += async (_, _) =>
            {
                // Stop as soon as the monitor is not visible any more.
                if (_monitorPage?.Core == null || !_monitorPage.IsActive || !_monitorPage.Url.Contains("/settings.html")) { StopMonitor(); return; }
                try { _monitorPage.PostMessage((await TabMonitor.SampleAsync()).ToJsonString()); }
                catch (Exception ex) { Log.Write($"Monitor failed: {ex.Message}"); }
            };
        }
        _monitorTimer.Start();
    }

    private static void StopMonitor()
    {
        _monitorTimer?.Stop();
        _monitorPage = null;
    }

    private static void TabAction(BrowserTab page, string type, string id)
    {
        foreach (var window in SessionManager.Windows)
        {
            var tab = window.Tabs.Tabs.FirstOrDefault(t => t.Id == id);
            if (tab == null) continue;
            switch (type)
            {
                case "tab.sleep": tab.Sleep(window.Tabs.Host); break;
                case "tab.close": window.Tabs.Close(tab); break;
                case "tab.activate": window.Tabs.Activate(tab); window.Activate(); break;
                case "tab.devtools":
                    if (tab.Core != null) tab.Core.OpenDevToolsWindow();
                    else { window.Tabs.Activate(tab); }
                    break;
                case "tab.blocked":
                    page.PostMessage(new JsonObject
                    {
                        ["type"] = "blockedList",
                        ["title"] = tab.DisplayTitle,
                        ["urls"] = new JsonArray(tab.BlockedRequests.Reverse().Select(u => (JsonNode)u!).ToArray()),
                    }.ToJsonString());
                    break;
            }
            return;
        }
    }

    // ---------------------------------------------------------------- backup

    private static void Export(BrowserTab page)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Exportar la configuración de Phast",
            FileName = $"phast-configuracion_{DateTime.Now:yyyy_MM_dd}.json",
            Filter = "Configuración de Phast (*.json)|*.json",
        };
        if (dialog.ShowDialog(SessionManager.LastActiveWindow) != true) return;
        ConfigBackup.Export(dialog.FileName);
        page.PostMessage(new JsonObject { ["type"] = "toast", ["text"] = "Configuración exportada" }.ToJsonString());
    }

    private static void Import(BrowserTab page)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Importar la configuración de Phast",
            Filter = "Configuración de Phast (*.json)|*.json|Todos los archivos|*.*",
        };
        if (dialog.ShowDialog(SessionManager.LastActiveWindow) != true) return;
        var error = ConfigBackup.Import(dialog.FileName);
        page.PostMessage(new JsonObject { ["type"] = "toast", ["text"] = error ?? "Configuración importada" }.ToJsonString());
    }
}
