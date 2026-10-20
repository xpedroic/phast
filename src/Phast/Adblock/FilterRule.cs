namespace Phast.Adblock;

/// <summary>Kinds of network requests, as used by Adblock Plus filter options ($script, $image...).</summary>
[Flags]
public enum ResourceType
{
    None = 0,
    Document = 1 << 0,
    Subdocument = 1 << 1,
    Stylesheet = 1 << 2,
    Script = 1 << 3,
    Image = 1 << 4,
    Font = 1 << 5,
    Media = 1 << 6,
    Xhr = 1 << 7,
    WebSocket = 1 << 8,
    Ping = 1 << 9,
    Object = 1 << 10,
    Other = 1 << 11,
    All = (1 << 12) - 1,
    /// <summary>Default for rules without type options: everything except the page itself.</summary>
    AllButDocument = All & ~Document,
}

public enum Party { Any, First, Third }

/// <summary>
/// One network filter in Adblock Plus syntax, e.g. "||ads.example.com^$third-party" or "@@/ads.js$domain=x.com".
/// Supported: ||domain anchors, | start/end anchors, * wildcards, ^ separators, @@ exceptions and the options
/// third-party / first-party, resource types (and ~negations), domain=, important, match-case, all, $document exceptions.
/// Rules with any other option (redirect, csp, removeparam...) are skipped on purpose: better to let something
/// through than to break a page with a half-understood rule.
/// </summary>
public sealed class NetRule
{
    public required string Pattern { get; init; }        // lower-case, anchors removed
    public bool IsException { get; init; }
    public bool Important { get; init; }
    public bool HostAnchor { get; init; }                // ||
    public bool StartAnchor { get; init; }               // |  at start
    public bool EndAnchor { get; init; }                 // |  at end
    public ResourceType Types { get; init; } = ResourceType.AllButDocument;
    public Party Party { get; init; }
    public string[]? IncludeDomains { get; init; }
    public string[]? ExcludeDomains { get; init; }
    /// <summary>"@@||site^$document": disables blocking on that whole site.</summary>
    public bool IsDocumentException { get; init; }
    /// <summary>For pure "||host^" rules: the host, set by the engine when indexing.</summary>
    public string Host { get; internal set; } = "";

    private string? _literal;
    /// <summary>Longest fixed piece of the pattern (no '*' or '^'): a matching URL must contain it.</summary>
    private string Literal => _literal ??= Pattern.Split('*', '^').MaxBy(s => s.Length) ?? "";

    /// <summary>Parses one line; returns null for comments, cosmetic rules and unsupported rules.</summary>
    public static NetRule? Parse(string line)
    {
        line = line.Trim();
        if (line.Length == 0 || line[0] is '!' or '[' || line.Contains('#') && IsCosmetic(line)) return null;

        var exception = line.StartsWith("@@", StringComparison.Ordinal);
        if (exception) line = line[2..];

        string pattern = line;
        string? options = null;
        var dollar = line.LastIndexOf('$');
        if (dollar >= 0)
        {
            options = line[(dollar + 1)..];
            pattern = line[..dollar];
        }

        // Regular expression rules are rare and slow: skip them.
        if (pattern.Length > 2 && pattern[0] == '/' && pattern[^1] == '/') return null;

        var party = Party.Any;
        var important = false;
        var isDocException = false;
        ResourceType include = ResourceType.None, exclude = ResourceType.None;
        string[]? incDomains = null, excDomains = null;

        if (options != null)
        {
            foreach (var raw in options.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var opt = raw.Trim().ToLowerInvariant();
                var neg = opt.StartsWith('~');
                var name = neg ? opt[1..] : opt;

                if (name.StartsWith("domain=", StringComparison.Ordinal) || name.StartsWith("from=", StringComparison.Ordinal))
                {
                    var list = name[(name.IndexOf('=') + 1)..].Split('|', StringSplitOptions.RemoveEmptyEntries);
                    incDomains = list.Where(d => !d.StartsWith('~')).ToArray();
                    excDomains = list.Where(d => d.StartsWith('~')).Select(d => d[1..]).ToArray();
                    if (incDomains.Any(d => d.Contains('*')) || excDomains.Any(d => d.Contains('*'))) return null; // "google.*" not supported
                    if (incDomains.Length == 0) incDomains = null;
                    if (excDomains.Length == 0) excDomains = null;
                    continue;
                }

                switch (name)
                {
                    case "third-party" or "3p": party = neg ? Party.First : Party.Third; continue;
                    case "first-party" or "1p": party = neg ? Party.Third : Party.First; continue;
                    case "important": important = true; continue;
                    case "match-case": continue;
                    case "all": include |= ResourceType.All; continue;
                    case "document" or "doc":
                        if (exception && !neg) { isDocException = true; continue; }
                        return null; // we never block whole pages
                }

                var type = TypeFromName(name);
                if (type == ResourceType.None) return null; // unsupported option (popup, redirect, csp, ...): skip rule
                if (neg) exclude |= type; else include |= type;
            }
        }

        var types = include != ResourceType.None ? include : ResourceType.AllButDocument;
        types &= ~exclude;
        if (types == ResourceType.None && !isDocException) return null;

        pattern = pattern.ToLowerInvariant();
        bool hostAnchor = false, startAnchor = false, endAnchor = false;
        if (pattern.StartsWith("||", StringComparison.Ordinal)) { hostAnchor = true; pattern = pattern[2..]; }
        else if (pattern.StartsWith('|')) { startAnchor = true; pattern = pattern[1..]; }
        if (pattern.EndsWith('|')) { endAnchor = true; pattern = pattern[..^1]; }

        // Leading/trailing wildcards add nothing (except cancelling anchors).
        if (pattern.StartsWith('*') && !hostAnchor) { pattern = pattern.TrimStart('*'); startAnchor = false; }
        if (pattern.EndsWith('*')) { pattern = pattern.TrimEnd('*'); endAnchor = false; }

        // An empty pattern, or just a scheme like "|https:", without domain restrictions would block everything: refuse it.
        if (incDomains == null && !isDocException &&
            (pattern.Length == 0 || startAnchor && pattern.TrimEnd('/') is "http:" or "https:" or "ws:" or "wss:"))
            return null;

        return new NetRule
        {
            Pattern = pattern,
            IsException = exception,
            Important = important,
            HostAnchor = hostAnchor,
            StartAnchor = startAnchor,
            EndAnchor = endAnchor,
            Types = isDocException ? ResourceType.All : types,
            Party = party,
            IncludeDomains = incDomains,
            ExcludeDomains = excDomains,
            IsDocumentException = isDocException,
        };
    }

    private static bool IsCosmetic(string line) =>
        line.Contains("##") || line.Contains("#@#") || line.Contains("#?#") || line.Contains("#$#") || line.Contains("#%#") || line.Contains("#@?#") || line.Contains("#@$#");

    private static ResourceType TypeFromName(string name) => name switch
    {
        "script" => ResourceType.Script,
        "image" => ResourceType.Image,
        "stylesheet" or "css" => ResourceType.Stylesheet,
        "subdocument" or "frame" => ResourceType.Subdocument,
        "xmlhttprequest" or "xhr" => ResourceType.Xhr,
        "websocket" => ResourceType.WebSocket,
        "ping" or "beacon" => ResourceType.Ping,
        "media" => ResourceType.Media,
        "font" => ResourceType.Font,
        "object" or "object-subrequest" => ResourceType.Object,
        "other" => ResourceType.Other,
        _ => ResourceType.None,
    };

    /// <summary>Pure "||host^" rule (the most common kind) that can go into the fast hostname table.</summary>
    public bool IsPureHost(out string host)
    {
        host = "";
        // Needs the trailing '^': "||example.com" alone would also match "example.community".
        if (!HostAnchor || EndAnchor || !Pattern.EndsWith('^')) return false;
        var p = Pattern[..^1];
        if (p.Length == 0 || p.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_'))) return false;
        host = p;
        return true;
    }

    /// <summary>Checks options (type, party, domains) — the pattern itself is checked by <see cref="MatchesUrl"/>.</summary>
    public bool MatchesContext(ResourceType type, bool thirdParty, string docHost)
    {
        if ((Types & type) == 0) return false;
        if (Party == Party.Third && !thirdParty) return false;
        if (Party == Party.First && thirdParty) return false;
        // Plain loops instead of LINQ: this runs for many rules on every request.
        if (ExcludeDomains != null)
            foreach (var d in ExcludeDomains)
                if (HostMatches(docHost, d)) return false;
        if (IncludeDomains != null)
        {
            foreach (var d in IncludeDomains)
                if (HostMatches(docHost, d)) return true;
            return false;
        }
        return true;
    }

    /// <summary>"sub.example.com" matches "example.com".</summary>
    public static bool HostMatches(string host, string domain) =>
        host.Length >= domain.Length && host.EndsWith(domain, StringComparison.Ordinal)
        && (host.Length == domain.Length || host[host.Length - domain.Length - 1] == '.');

    /// <summary>Does the (lower-case) URL match this rule's pattern?</summary>
    public bool MatchesUrl(string url, int hostStart, int hostEnd)
    {
        // Cheap (vectorized) pre-check before the full wildcard match.
        var literal = Literal;
        if (literal.Length > 0 && !url.Contains(literal, StringComparison.Ordinal)) return false;

        if (HostAnchor)
        {
            // The pattern must start at the beginning of the host or right after one of its dots.
            for (int i = hostStart; i < hostEnd; i++)
            {
                if ((i == hostStart || url[i - 1] == '.') && Glob.Match(Pattern, url, i, leadingStar: false, EndAnchor))
                    return true;
            }
            return false;
        }
        return Glob.Match(Pattern, url, 0, leadingStar: !StartAnchor, EndAnchor);
    }
}

/// <summary>Wildcard matcher for filter patterns: '*' = anything, '^' = separator character or end of URL.</summary>
public static class Glob
{
    public static bool IsSeparator(char c) => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '%');

    public static bool Match(string p, string s, int si, bool leadingStar, bool endAnchor)
    {
        int pi = 0;
        int restartP = leadingStar ? 0 : -1;  // pattern position to restart from after a '*'
        int starS = si;                      // string position the current '*' has consumed up to
        while (true)
        {
            if (pi == p.Length)
            {
                if (!endAnchor || si == s.Length) return true;
            }
            else
            {
                var c = p[pi];
                if (c == '*') { restartP = ++pi; starS = si; continue; }
                if (c == '^')
                {
                    if (si == s.Length) { pi++; continue; }
                    if (IsSeparator(s[si])) { pi++; si++; continue; }
                }
                else if (si < s.Length && s[si] == c) { pi++; si++; continue; }
            }
            // Mismatch: let the last '*' swallow one more character and retry.
            if (restartP < 0 || starS >= s.Length) return false;
            si = ++starS;
            pi = restartP;
        }
    }
}
