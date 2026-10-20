using System.IO;
using System.Reflection;

namespace Phast.Core;

/// <summary>
/// Internal pages (new tab, settings, reader...) are plain HTML/CSS/JS files embedded in the exe.
/// On startup they are extracted to %LOCALAPPDATA%\Phast\Pages and served by WebView2 as
/// https://phast.internal/... through SetVirtualHostNameToFolderMapping.
/// The address bar shows them with the friendly scheme phast://name.
/// </summary>
public static class InternalPages
{
    public const string Host = "phast.internal";
    public const string Origin = "https://" + Host;
    public const string Scheme = "phast://";

    public const string NewTab = Scheme + "newtab";

    /// <summary>Extracts the embedded pages if this build's pages are not on disk yet.</summary>
    public static void EnsureExtracted()
    {
        var asm = Assembly.GetExecutingAssembly();
        // The module version id changes on every build, so updated pages are always re-extracted.
        var stamp = asm.ManifestModule.ModuleVersionId.ToString();
        var stampFile = Path.Combine(AppPaths.Pages, ".version");
        if (File.Exists(stampFile) && File.ReadAllText(stampFile) == stamp)
            return;

        if (Directory.Exists(AppPaths.Pages))
            Directory.Delete(AppPaths.Pages, recursive: true);

        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("Pages/", StringComparison.Ordinal)) continue;
            var target = Path.Combine(AppPaths.Pages, name["Pages/".Length..].Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var src = asm.GetManifestResourceStream(name)!;
            using var dst = File.Create(target);
            src.CopyTo(dst);
        }
        File.WriteAllText(stampFile, stamp);
    }

    public static bool IsInternal(string? url) =>
        url != null && (url.StartsWith(Origin + "/", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase));

    /// <summary>phast://settings#search  ->  https://phast.internal/settings.html#search</summary>
    public static string ToRealUrl(string phastUrl)
    {
        var rest = phastUrl[Scheme.Length..];
        var cut = rest.IndexOfAny(['?', '#']);
        var name = (cut < 0 ? rest : rest[..cut]).TrimEnd('/');
        var tail = cut < 0 ? "" : rest[cut..];
        if (string.IsNullOrEmpty(name)) name = "newtab";
        return $"{Origin}/{name}.html{tail}";
    }

    /// <summary>https://phast.internal/settings.html#search  ->  phast://settings#search</summary>
    public static string ToDisplayUrl(string url)
    {
        if (!url.StartsWith(Origin + "/", StringComparison.OrdinalIgnoreCase)) return url;
        var rest = url[(Origin.Length + 1)..];
        var cut = rest.IndexOfAny(['?', '#']);
        var name = cut < 0 ? rest : rest[..cut];
        var tail = cut < 0 ? "" : rest[cut..];
        if (name.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) name = name[..^5];
        return Scheme + name + tail;
    }

    public static bool IsNewTab(string? url) =>
        url != null && (url.Equals(NewTab, StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith(Origin + "/newtab.html", StringComparison.OrdinalIgnoreCase));
}
