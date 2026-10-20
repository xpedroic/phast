using System.Diagnostics;
using Phast.Session;

namespace Phast.Core;

/// <summary>
/// Restarts Phast (needed after changing engine flags or GPU acceleration): saves the session, starts a new
/// process that waits for this one (and its engine) to exit, then closes everything.
/// </summary>
public static class AppRestart
{
    public const string Argument = "--restart-after=";

    public static void Restart()
    {
        SessionManager.SaveNow();
        SingleInstance.Release();
        var exe = Environment.ProcessPath!;
        Process.Start(new ProcessStartInfo(exe, Argument + Environment.ProcessId) { UseShellExecute = false });
        SessionManager.ExitAll();
    }

    /// <summary>Called by the new process: waits (max 15 s) for the old one so the engine data folder is free.</summary>
    public static void WaitForPrevious(string arg)
    {
        if (!int.TryParse(arg[Argument.Length..], out var pid)) return;
        try
        {
            using var old = Process.GetProcessById(pid);
            old.WaitForExit(15_000);
        }
        catch (ArgumentException) { /* already gone */ }
        // The engine's own processes may need a moment longer to release the profile.
        Thread.Sleep(500);
    }
}
