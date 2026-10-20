using System.Net;
using System.Text.RegularExpressions;
using Phast.Settings;

namespace Phast.Core;

/// <summary>
/// Turns whatever the user typed in the address bar into a URL to navigate to:
/// a full URL, a bare domain ("github.com"), an internal page ("phast://settings") or a web search.
/// </summary>
public static partial class UrlResolver
{
    private static readonly string[] KnownSchemes =
        ["http:", "https:", "file:", "about:", "data:", "view-source:", "edge:", "chrome:", "ftp:", "mailto:"];

    // host[:port][/path...] where host is a dotted name with a 2+ letter TLD.
    [GeneratedRegex(@"^[\w\-]+(\.[\w\-]+)*\.[a-z]{2,63}(:\d{1,5})?([/?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DomainLike();

    [GeneratedRegex(@"^(localhost|[\w\-]+\.localhost)(:\d{1,5})?([/?#].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex LocalhostLike();

    public static string? Resolve(string input)
    {
        var text = input.Trim();
        if (text.Length == 0) return null;

        if (text.StartsWith(InternalPages.Scheme, StringComparison.OrdinalIgnoreCase))
            return InternalPages.ToRealUrl(text);

        if (KnownSchemes.Any(s => text.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
            return text;

        if (!text.Contains(' '))
        {
            // Windows paths, e.g. C:\folder\file.html
            if (text.Length > 2 && text[1] == ':' && (text[2] == '\\' || text[2] == '/'))
                return new Uri(text).AbsoluteUri;

            if (LocalhostLike().IsMatch(text) || IsIpWithOptionalPort(text))
                return "http://" + text;

            if (DomainLike().IsMatch(text))
                return "https://" + text;
        }

        return SearchEngines.Get(SettingsService.Current.SearchEngine).BuildUrl(text);
    }

    private static bool IsIpWithOptionalPort(string text)
    {
        var hostPart = text.Split('/', '?', '#')[0];
        var colon = hostPart.LastIndexOf(':');
        if (colon > 0 && !hostPart.StartsWith('[')) hostPart = hostPart[..colon];
        return IPAddress.TryParse(hostPart.Trim('[', ']'), out _) && hostPart.Contains('.') || hostPart.StartsWith('[');
    }

    /// <summary>The text shown in the address bar for a given real URL.</summary>
    public static string ToDisplay(string? url)
    {
        if (string.IsNullOrEmpty(url) || url == "about:blank") return "";
        if (InternalPages.IsNewTab(url)) return "";
        return InternalPages.ToDisplayUrl(url);
    }
}
