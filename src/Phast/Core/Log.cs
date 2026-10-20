using System.IO;

namespace Phast.Core;

/// <summary>
/// Optional diagnostic log, written to %LOCALAPPDATA%\Phast\debug.log only when the
/// environment variable PHAST_LOG=1 is set. Costs nothing otherwise.
/// </summary>
public static class Log
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("PHAST_LOG") == "1";
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        System.Diagnostics.Debug.WriteLine(message);
        if (!Enabled) return;
        lock (Sync)
        {
            try { File.AppendAllText(Path.Combine(AppPaths.Root, "debug.log"), $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}"); }
            catch { /* logging must never break the browser */ }
        }
    }
}
