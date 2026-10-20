using System.IO;
using System.Windows.Threading;

namespace Phast.Core;

/// <summary>Remembers the zoom chosen for each site (zoom.json), like Chrome does.</summary>
public static class ZoomMemory
{
    private static Dictionary<string, double>? _zooms;
    private static DispatcherTimer? _saveTimer;
    private static string FilePath => Path.Combine(AppPaths.Root, "zoom.json");
    private static Dictionary<string, double> Zooms => _zooms ??= JsonFile.LoadOrDefault<Dictionary<string, double>>(FilePath);

    public static double Get(string? host) =>
        !string.IsNullOrEmpty(host) && Zooms.TryGetValue(host, out var z) && z is > 0.2 and < 5.1 ? z : 1;

    public static void Set(string? host, double zoom)
    {
        if (string.IsNullOrEmpty(host) || host == InternalPages.Host) return;
        if (Math.Abs(zoom - 1) < 0.001) { if (!Zooms.Remove(host)) return; }
        else if (Zooms.TryGetValue(host, out var old) && Math.Abs(old - zoom) < 0.001) return;
        else Zooms[host] = Math.Round(zoom, 3);
        ScheduleSave();
    }

    private static void ScheduleSave()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); JsonFile.Save(FilePath, Zooms); };
        }
        _saveTimer.Stop();
        _saveTimer.Start();
    }
}
