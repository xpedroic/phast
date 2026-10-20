using System.IO;
using System.IO.Pipes;
using System.Text;
using Phast.Interop;

namespace Phast.Core;

/// <summary>
/// Makes sure only one Phast process runs per user session. A second launch forwards its
/// command-line arguments (URLs) to the first process through a named pipe and exits.
/// </summary>
public static class SingleInstance
{
    // One instance per user and data folder (a custom PHAST_DATA_DIR gets its own instance).
    private static readonly string Id = Environment.GetEnvironmentVariable("PHAST_DATA_DIR") is { Length: > 0 }
        ? $"Phast-{Environment.UserName}-{StableHash(AppPaths.Root.ToLowerInvariant()):x16}"
        : $"Phast-{Environment.UserName}";

    /// <summary>FNV-1a: unlike string.GetHashCode it is the same in every process.</summary>
    private static ulong StableHash(string s)
    {
        ulong h = 14695981039346656037UL;
        foreach (var c in s) { h ^= c; h *= 1099511628211UL; }
        return h;
    }
    private static readonly string MutexName = $@"Local\{Id}";
    private static readonly string PipeName = Id;
    private static Mutex? _mutex;

    /// <summary>Raised on a background thread when another launch sent its arguments.</summary>
    public static event Action<string[]>? ArgumentsReceived;

    /// <summary>Returns true if this is the first instance. Otherwise forwards <paramref name="args"/> and returns false.</summary>
    public static bool TryAcquire(string[] args)
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (createdNew)
        {
            _ = Task.Run(ListenLoop);
            return true;
        }

        try
        {
            // Let the first instance bring its window to the foreground.
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            using var writer = new StreamWriter(client, Encoding.UTF8);
            writer.Write(string.Join("\n", args));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not reach running instance: {ex.Message}");
        }
        return false;
    }

    /// <summary>Lets a new Phast process become the main instance (used when restarting).</summary>
    public static void Release()
    {
        try { _mutex?.ReleaseMutex(); } catch (ApplicationException) { /* not owned */ }
        _mutex?.Dispose();
        _mutex = null;
    }

    private static async Task ListenLoop()
    {
        while (true)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var text = await reader.ReadToEndAsync();
                var args = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                ArgumentsReceived?.Invoke(args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pipe error: {ex.Message}");
                await Task.Delay(500);
            }
        }
    }
}
