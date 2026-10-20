using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

// Tests share static services (settings, bookmarks, history): run them one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Phast.Tests;

/// <summary>Runs before any test: every test uses a throw-away data folder, never the user's real Phast data.</summary>
internal static class TestSetup
{
    public static readonly string DataDir = Path.Combine(Path.GetTempPath(), "PhastUnitTests-" + Environment.ProcessId);

    [ModuleInitializer]
    internal static void Init()
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("PHAST_DATA_DIR", DataDir);
    }

    /// <summary>Runs code on an STA thread (WPF objects such as panels require it).</summary>
    public static void OnSta(Action action)
    {
        Exception? error = null;
        var t = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (error != null) ExceptionDispatchInfoHelper.Throw(error);
    }

    private static class ExceptionDispatchInfoHelper
    {
        public static void Throw(Exception ex) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
