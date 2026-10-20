using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Phast.Interop;

/// <summary>
/// Gives memory back to Windows when Phast is not in use (window minimized): a compacting GC plus
/// trimming the process working set. Pages that are needed again are reloaded from RAM/standby quickly.
/// </summary>
internal static partial class MemoryTrim
{
    private static DispatcherTimer? _timer;

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessWorkingSetSize(IntPtr process, nint min, nint max);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    /// <summary>Trims a couple of seconds later (after the minimize animation and pending work).</summary>
    public static void TrimSoon()
    {
        _timer ??= CreateTimer();
        _timer.Stop();
        _timer.Start();
    }

    private static DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromSeconds(2) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            GC.Collect(2, GCCollectionMode.Optimized, blocking: false, compacting: true);
            SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
        };
        return t;
    }
}
