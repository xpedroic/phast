using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Phast.Core;
using Phast.Session;
using Phast.Settings;

namespace Phast.Downloads;

/// <summary>A download (running or finished). Finished ones are remembered in downloads.json.</summary>
public sealed class DownloadItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..10];
    public string Url { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string State { get; set; } = "progress";   // progress | paused | done | failed | canceled
    public long Received { get; set; }
    public long Total { get; set; }
    public DateTime Started { get; set; } = DateTime.Now;
    public string? Error { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] public CoreWebView2DownloadOperation? Operation { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public bool FileExists => File.Exists(FilePath);
}

/// <summary>
/// Phast's own downloads list (phast://downloads, Ctrl+J): replaces the engine's download flyout, saves to the
/// chosen folder (or asks), and keeps the history of finished downloads.
/// </summary>
public static class DownloadManager
{
    private const int MaxRemembered = 200;
    private static List<DownloadItem>? _items;
    private static string FilePath => Path.Combine(AppPaths.Root, "downloads.json");

    public static List<DownloadItem> Items => _items ??= JsonFile.LoadOrDefault<List<DownloadItem>>(FilePath);

    /// <summary>Raised (UI thread) when a download starts, progresses or finishes.</summary>
    public static event Action? Changed;

    public static int ActiveCount => Items.Count(i => i.State is "progress" or "paused");

    /// <summary>0..1 progress of the running downloads (for the toolbar ring), or -1 if none.</summary>
    public static double OverallProgress
    {
        get
        {
            var active = Items.Where(i => i.State == "progress" && i.Total > 0).ToList();
            if (active.Count == 0) return -1;
            return (double)active.Sum(i => i.Received) / active.Sum(i => i.Total);
        }
    }

    public static string DefaultFolder =>
        SettingsService.Current.DownloadFolder is { Length: > 0 } f && Directory.Exists(f) ? f : KnownFolders.Downloads;

    /// <summary>Hooked to every tab's CoreWebView2.DownloadStarting.</summary>
    public static void OnDownloadStarting(CoreWebView2DownloadStartingEventArgs e)
    {
        e.Handled = true; // no engine flyout: Phast shows its own list
        var deferral = e.GetDeferral();
        try
        {
            var name = Path.GetFileName(e.ResultFilePath);
            var target = UniquePath(Path.Combine(DefaultFolder, name));
            if (SettingsService.Current.AskWhereToSave)
            {
                var dialog = new Microsoft.Win32.SaveFileDialog { FileName = name, InitialDirectory = DefaultFolder, Title = "Guardar como" };
                if (dialog.ShowDialog(SessionManager.LastActiveWindow) != true) { e.Cancel = true; return; }
                target = dialog.FileName;
            }
            e.ResultFilePath = target;
            Track(e.DownloadOperation, target);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static void Track(CoreWebView2DownloadOperation op, string path)
    {
        var item = new DownloadItem { Url = op.Uri, FilePath = path, Total = (long)(op.TotalBytesToReceive ?? 0), Operation = op };
        Items.Insert(0, item);
        op.BytesReceivedChanged += (_, _) =>
        {
            item.Received = op.BytesReceived;
            item.Total = (long)(op.TotalBytesToReceive ?? 0);
            Changed?.Invoke();
        };
        op.StateChanged += (_, _) =>
        {
            item.FilePath = op.ResultFilePath;
            switch (op.State)
            {
                case CoreWebView2DownloadState.Completed:
                    item.State = "done"; item.Received = op.BytesReceived; item.Operation = null; break;
                case CoreWebView2DownloadState.Interrupted:
                    var reason = op.InterruptReason;
                    item.State = reason == CoreWebView2DownloadInterruptReason.UserCanceled ? "canceled"
                        : op.CanResume ? "paused" : "failed";
                    if (item.State == "failed") { item.Error = reason.ToString(); item.Operation = null; }
                    break;
                default:
                    item.State = "progress"; break;
            }
            Save();
            Changed?.Invoke();
        };
        Save();
        Changed?.Invoke();
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    // ---------------------------------------------------------------- actions (from phast://downloads)

    public static void Act(string id, string action)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item == null) return;
        try
        {
            switch (action)
            {
                case "pause": item.Operation?.Pause(); item.State = "paused"; break;
                case "resume": if (item.Operation?.CanResume == true) { item.Operation.Resume(); item.State = "progress"; } break;
                case "cancel": item.Operation?.Cancel(); item.State = "canceled"; item.Operation = null; break;
                case "open": if (item.FileExists) Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true }); break;
                case "folder":
                    if (item.FileExists) Process.Start("explorer.exe", $"/select,\"{item.FilePath}\"");
                    else if (Directory.Exists(Path.GetDirectoryName(item.FilePath))) Process.Start("explorer.exe", $"\"{Path.GetDirectoryName(item.FilePath)}\"");
                    break;
                case "remove": Items.Remove(item); break;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        {
            Log.Write($"Download action {action} failed: {ex.Message}");
        }
        Save();
        Changed?.Invoke();
    }

    /// <summary>Removes finished/failed/canceled entries from the list (files are kept).</summary>
    public static void ClearFinished()
    {
        Items.RemoveAll(i => i.State is not ("progress" or "paused"));
        Save();
        Changed?.Invoke();
    }

    private static void Save()
    {
        try { JsonFile.Save(FilePath, Items.Where(i => i.State != "progress").Take(MaxRemembered).ToList()); }
        catch (IOException ex) { Log.Write($"Downloads save failed: {ex.Message}"); }
    }

    public static JsonObject ToJson() => new()
    {
        ["type"] = "downloads",
        ["folder"] = DefaultFolder,
        ["items"] = new JsonArray(Items.Select(i => (JsonNode)new JsonObject
        {
            ["id"] = i.Id, ["url"] = i.Url, ["name"] = Path.GetFileName(i.FilePath), ["path"] = i.FilePath,
            ["state"] = i.State, ["received"] = i.Received, ["total"] = i.Total,
            ["started"] = i.Started.ToString("s"), ["exists"] = i.FileExists, ["canResume"] = i.Operation?.CanResume == true,
        }).ToArray()),
    };
}

/// <summary>The user's real Downloads folder (it can be moved, so ask Windows instead of guessing).</summary>
internal static partial class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    [System.Runtime.InteropServices.LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderPath(in Guid id, uint flags, IntPtr token, out IntPtr path);

    public static string Downloads
    {
        get
        {
            if (SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out var ptr) == 0)
            {
                try { return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr)!; }
                finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(ptr); }
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
