using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Phast.Core;
using Phast.Session;
using Phast.Tabs;

namespace Phast.Developer;

/// <summary>
/// Real-time memory and CPU per tab (Settings > Advanced > Monitor). Only runs while the monitor is on screen.
/// The engine reports which process renders each tab; several tabs of the same site can share one process.
/// </summary>
public static class TabMonitor
{
    private static readonly Dictionary<int, (TimeSpan Cpu, DateTime At)> LastCpu = new();

    private static (long Memory, double Cpu) Measure(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            var now = DateTime.UtcNow;
            var cpu = p.TotalProcessorTime;
            double percent = 0;
            if (LastCpu.TryGetValue(pid, out var last) && (now - last.At).TotalMilliseconds > 100)
                percent = (cpu - last.Cpu).TotalMilliseconds / (now - last.At).TotalMilliseconds / Environment.ProcessorCount * 100;
            LastCpu[pid] = (cpu, now);
            return (p.PrivateMemorySize64, Math.Max(0, percent));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return (0, 0);
        }
    }

    private static string KindName(CoreWebView2ProcessKind kind) => kind switch
    {
        CoreWebView2ProcessKind.Browser => "Navegador (motor)",
        CoreWebView2ProcessKind.Renderer => "Páginas",
        CoreWebView2ProcessKind.Gpu => "GPU",
        CoreWebView2ProcessKind.Utility => "Utilidad",
        CoreWebView2ProcessKind.SandboxHelper => "Ayudante",
        CoreWebView2ProcessKind.PpapiPlugin or CoreWebView2ProcessKind.PpapiBroker => "Complemento",
        _ => "Otro",
    };

    public static async Task<JsonObject> SampleAsync()
    {
        var env = await BrowserEnvironment.GetAsync();
        var infos = await env.GetProcessExtendedInfosAsync();

        // Main frame id -> renderer process id.
        var frameToPid = new Dictionary<uint, int>();
        var processes = new JsonArray();
        long total = 0;
        foreach (var info in infos)
        {
            var pid = info.ProcessInfo.ProcessId;
            var (mem, cpu) = Measure(pid);
            total += mem;
            foreach (var f in info.AssociatedFrameInfos)
                if (f.ParentFrameInfo == null) frameToPid[f.FrameId] = pid;
            processes.Add(new JsonObject
            {
                ["pid"] = pid, ["kind"] = KindName(info.ProcessInfo.Kind), ["memory"] = mem, ["cpu"] = Math.Round(cpu, 1),
                ["frames"] = info.AssociatedFrameInfos.Count,
            });
        }

        // Phast's own window process.
        var (selfMem, selfCpu) = Measure(Environment.ProcessId);
        total += selfMem;

        var tabs = new JsonArray();
        var pidUse = new Dictionary<int, int>();
        var rows = new List<(BrowserTab Tab, int Pid)>();
        foreach (var window in SessionManager.Windows)
            foreach (var tab in window.Tabs.Tabs)
            {
                var pid = 0;
                if (tab.Core != null && frameToPid.TryGetValue(tab.Core.FrameId, out var p)) pid = p;
                if (pid != 0) pidUse[pid] = pidUse.GetValueOrDefault(pid) + 1;
                rows.Add((tab, pid));
            }
        foreach (var (tab, pid) in rows)
        {
            var proc = pid != 0 ? processes.FirstOrDefault(x => (int)x!["pid"]! == pid) : null;
            tabs.Add(new JsonObject
            {
                ["id"] = tab.Id,
                ["title"] = tab.DisplayTitle,
                ["url"] = tab.Url,
                ["state"] = tab.StateLabel,
                ["active"] = tab.IsActive,
                ["pid"] = pid,
                ["memory"] = proc?["memory"]?.GetValue<long>() ?? 0,
                ["cpu"] = proc?["cpu"]?.GetValue<double>() ?? 0,
                ["shared"] = pid != 0 ? pidUse[pid] : 0,
                ["blocked"] = tab.BlockedCount,
            });
        }

        return new JsonObject
        {
            ["type"] = "monitor",
            ["tabs"] = tabs,
            ["processes"] = processes,
            ["phast"] = new JsonObject { ["memory"] = selfMem, ["cpu"] = Math.Round(selfCpu, 1) },
            ["total"] = total,
        };
    }
}
