using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Phast.Core;

/// <summary>
/// Remembers one favicon per site (host) on disk (%LOCALAPPDATA%\Phast\Favicons\host.png),
/// so bookmarks and suggestions can show icons without loading the page.
/// Images are decoded lazily and kept in memory once used.
/// </summary>
public static class FaviconCache
{
    private static readonly Dictionary<string, ImageSource?> Memory = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised with the host whose icon was stored (UI thread when saved from a tab).</summary>
    public static event Action<string>? Saved;

    private static string Folder => Path.Combine(AppPaths.Root, "Favicons");

    public static string? HostOf(string? url) =>
        url != null && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")
            ? u.Host.ToLowerInvariant() : null;

    private static string FileFor(string host) =>
        Path.Combine(Folder, string.Concat(host.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-')) + ".png");

    /// <summary>Stores the favicon of a page (called when a tab receives its icon).</summary>
    public static void Save(string? url, byte[] png, bool overwrite = true, bool notify = true)
    {
        var host = HostOf(url);
        if (host == null || png.Length == 0 || png.Length > 64_000) return;
        try
        {
            Directory.CreateDirectory(Folder);
            var file = FileFor(host);
            if (!overwrite && File.Exists(file)) return;
            if (File.Exists(file) && new FileInfo(file).Length == png.Length) return; // unchanged
            File.WriteAllBytes(file, png);
            lock (Memory) Memory.Remove(host);
            if (notify) Saved?.Invoke(host);
        }
        catch (IOException) { /* best effort */ }
    }

    public static bool Has(string? url) => HostOf(url) is { } host && File.Exists(FileFor(host));

    /// <summary>Favicon for a URL's site, or null if unknown.</summary>
    public static ImageSource? Get(string? url)
    {
        var host = HostOf(url);
        if (host == null) return null;
        lock (Memory)
            if (Memory.TryGetValue(host, out var cached)) return cached;

        ImageSource? image = null;
        var file = FileFor(host);
        if (File.Exists(file))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(file);
                bmp.DecodePixelWidth = 32;
                bmp.EndInit();
                bmp.Freeze();
                image = bmp;
            }
            catch { image = null; }
        }
        lock (Memory) Memory[host] = image;
        return image;
    }

    /// <summary>Favicon as a data: URL for internal pages (null if unknown).</summary>
    public static string? GetDataUrl(string? url)
    {
        var host = HostOf(url);
        if (host == null) return null;
        var file = FileFor(host);
        try { return File.Exists(file) ? "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(file)) : null; }
        catch (IOException) { return null; }
    }
}
