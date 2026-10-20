namespace Phast.Adblock;

/// <summary>
/// Compiled, read-only set of filters. Built once from the filter lists (in the background) and then
/// queried for every network request, so lookups must be very cheap (a few microseconds):
/// - "||host^" rules live in a hash table keyed by hostname, checked for the request host and each of its
///   parent domains, hashing spans of the URL (no string allocations);
/// - other rules are indexed by one "token" (a whole word of their pattern). Each rule is filed under its
///   rarest word, so buckets stay small, and only buckets whose word appears in the URL are tested;
/// - the few rules without any usable word are tested for every request.
/// Call <see cref="Compile"/> after adding lists (it is also done automatically on first use). Thread-safe for reads.
/// </summary>
public sealed class FilterEngine
{
    private readonly Dictionary<ulong, List<NetRule>> _blockHosts = new();
    private readonly Dictionary<ulong, List<NetRule>> _allowHosts = new();
    private readonly Dictionary<ulong, List<NetRule>> _blockTokens = new();
    private readonly Dictionary<ulong, List<NetRule>> _allowTokens = new();
    private readonly List<NetRule> _blockGeneric = [];
    private readonly List<NetRule> _allowGeneric = [];
    private readonly List<NetRule> _documentExceptions = [];
    private readonly List<NetRule> _pending = [];          // token rules waiting for Compile()
    private volatile bool _compiled;
    private bool _hasImportant;

    // Cosmetic (element hiding) rules.
    // Site-specific ones are applied in full. Generic ones ("##.ad-box") are indexed by their leading class/id so a page
    // only receives the selectors for the classes/ids it actually contains (like uBlock Origin); the few generic
    // selectors without a leading class/id ("##[data-ad-slot]") are always applied.
    private readonly Dictionary<string, List<string>> _hide = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _unhide = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _genericByToken = new(StringComparer.Ordinal);
    private readonly List<string> _highGeneric = [];
    private readonly HashSet<string> _genericHideSites = new(StringComparer.Ordinal);
    private readonly HashSet<string> _elemHideSites = new(StringComparer.Ordinal);

    // Words too common to be useful as an index key.
    private static readonly HashSet<string> BadTokens = ["http", "https", "www", "com", "net", "org", "html", "js", "php", "jpg", "png", "gif"];

    public int NetworkRuleCount { get; private set; }
    public int CosmeticRuleCount { get; private set; }

    // ------------------------------------------------------------------ building

    public void AddList(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '!' || line[0] == '[') continue;
            if (TryAddCosmetic(line)) continue;
            var rule = NetRule.Parse(line);
            if (rule != null) AddRule(rule);
        }
    }

    public void AddRule(NetRule rule)
    {
        // Cosmetic exceptions only switch element hiding off for a site; they must not allow network requests.
        if (rule.IsGenericHideException || rule.IsElemHideException)
        {
            if (rule.AnchorHost.Length > 0)
                (rule.IsElemHideException ? _elemHideSites : _genericHideSites).Add(rule.AnchorHost);
            return;
        }

        NetworkRuleCount++;
        _compiled = false;
        if (rule.IsDocumentException) { _documentExceptions.Add(rule); return; }
        if (rule.Important && !rule.IsException) _hasImportant = true;

        if (rule.IsPureHost(out var host))
        {
            rule.Host = host;
            var hosts = rule.IsException ? _allowHosts : _blockHosts;
            var key = Hash(host);
            if (!hosts.TryGetValue(key, out var list)) hosts[key] = list = [];
            list.Add(rule);
            return;
        }
        _pending.Add(rule);
    }

    /// <summary>Files every pending rule under its rarest safe word (or the generic list if it has none).</summary>
    public void Compile()
    {
        lock (_pending)
        {
            if (_compiled) return;
            var candidates = _pending.Select(r => (Rule: r, Tokens: SafeTokens(r))).ToList();

            var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (_, tokens) in candidates)
                foreach (var t in tokens) frequency[t] = frequency.GetValueOrDefault(t) + 1;

            foreach (var (rule, tokens) in candidates)
            {
                if (tokens.Count == 0)
                {
                    (rule.IsException ? _allowGeneric : _blockGeneric).Add(rule);
                    continue;
                }
                var best = tokens.OrderBy(t => frequency[t]).ThenByDescending(t => t.Length).First();
                var index = rule.IsException ? _allowTokens : _blockTokens;
                var key = Hash(best);
                if (!index.TryGetValue(key, out var list)) index[key] = list = [];
                list.Add(rule);
            }
            _pending.Clear();
            _compiled = true;
        }
    }

    /// <summary>
    /// Words of the pattern that are guaranteed to appear as whole words in any matching URL
    /// (i.e. not next to a '*' and not at an unanchored end of the pattern).
    /// </summary>
    private static List<string> SafeTokens(NetRule rule)
    {
        var p = rule.Pattern;
        var result = new List<string>();
        int i = 0;
        while (i < p.Length)
        {
            if (!IsTokenChar(p[i])) { i++; continue; }
            var start = i;
            while (i < p.Length && IsTokenChar(p[i])) i++;
            var end = i;

            var startOk = start > 0 ? p[start - 1] != '*' : rule.HostAnchor || rule.StartAnchor;
            var endOk = end < p.Length ? p[end] != '*' : rule.EndAnchor;
            if (!startOk || !endOk) continue;

            var token = p[start..end];
            if (token.Length >= 2 && !BadTokens.Contains(token)) result.Add(token);
        }
        return result;
    }

    private static bool IsTokenChar(char c) => char.IsAsciiLetterOrDigit(c) || c == '%';

    private static ulong Hash(ReadOnlySpan<char> s)
    {
        ulong h = 14695981039346656037UL; // FNV-1a
        foreach (var c in s) { h ^= c; h *= 1099511628211UL; }
        return h;
    }

    private bool TryAddCosmetic(string line)
    {
        int sep = line.IndexOf("##", StringComparison.Ordinal), sepLen = 2;
        var exception = false;
        var at = line.IndexOf("#@#", StringComparison.Ordinal);
        if (at >= 0 && (sep < 0 || at < sep)) { sep = at; sepLen = 3; exception = true; }
        if (sep < 0) return line.Contains("#?#") || line.Contains("#$#") || line.Contains("#%#") || line.Contains("#@?#") || line.Contains("#@$#");

        var domains = line[..sep];
        var selector = line[(sep + sepLen)..].Trim();
        // Skip scriptlets (+js), HTML filters (^) and procedural/extended selectors.
        if (selector.Length == 0 || selector.StartsWith('+') || selector.StartsWith('^')
            || selector.Contains(":-abp-") || selector.Contains(":has-text(") || selector.Contains(":style(")
            || selector.Contains(":xpath(") || selector.Contains(":matches-") || selector.Contains(":upward(")
            || selector.Contains(":remove(") || selector.Contains(":watch-attr(") || selector.Contains(":min-text-length("))
            return true;

        if (domains.Length == 0)
        {
            if (!exception) AddGenericSelector(selector); // generic exceptions without a site are very rare: ignored
            return true;
        }

        foreach (var d in domains.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var domain = d.Trim().ToLowerInvariant();
            if (domain.StartsWith('~') || domain.Contains('*') || domain.Contains('/')) continue;
            if (exception)
            {
                if (!_unhide.TryGetValue(domain, out var set)) _unhide[domain] = set = [];
                set.Add(selector);
            }
            else
            {
                if (!_hide.TryGetValue(domain, out var list)) _hide[domain] = list = [];
                list.Add(selector);
                CosmeticRuleCount++;
            }
        }
        return true;
    }

    private void AddGenericSelector(string selector)
    {
        CosmeticRuleCount++;
        var key = LeadingToken(selector);
        if (key == null) { _highGeneric.Add(selector); return; }
        if (!_genericByToken.TryGetValue(key, out var list)) _genericByToken[key] = list = [];
        list.Add(selector);
    }

    /// <summary>".ad-box > div" → ".ad-box"; "#banner" → "#banner"; anything else → null.</summary>
    private static string? LeadingToken(string selector)
    {
        if (selector.Length < 2 || selector[0] is not ('.' or '#')) return null;
        var end = 1;
        while (end < selector.Length && (char.IsAsciiLetterOrDigit(selector[end]) || selector[end] is '-' or '_')) end++;
        if (end == 1) return null;
        if (end < selector.Length && selector[end] == '\\') return null; // escaped characters: keep it simple
        return selector[..end];
    }

    public int GenericSelectorCount => _genericByToken.Values.Sum(l => l.Count) + _highGeneric.Count;

    private static bool SiteIn(HashSet<string> sites, string docHost)
    {
        for (var h = docHost; ; )
        {
            if (sites.Contains(h)) return true;
            var dot = h.IndexOf('.');
            if (dot < 0) return false;
            h = h[(dot + 1)..];
        }
    }

    // ------------------------------------------------------------------ querying

    /// <summary>Is the whole site exempt from blocking by the lists ("@@||site^$document")?</summary>
    public bool IsSiteExempt(string docHost)
    {
        foreach (var r in _documentExceptions)
            if (r.IsPureHost(out var h) && NetRule.HostMatches(docHost, h)) return true;
        return false;
    }

    /// <summary>Returns the blocking rule that matches this request, or null if it is allowed.</summary>
    public NetRule? Match(string url, string docHost, ResourceType type)
    {
        if (!_compiled) Compile();

        var lower = url.ToLowerInvariant();
        var schemeEnd = lower.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0) return null;
        var hostStart = schemeEnd + 3;
        var hostEnd = hostStart;
        while (hostEnd < lower.Length && lower[hostEnd] is not ('/' or '?' or '#' or ':')) hostEnd++;
        var at = lower.LastIndexOf('@', hostEnd - 1, hostEnd - hostStart);   // user:pass@host
        if (at >= 0) hostStart = at + 1;
        var host = lower.AsSpan(hostStart, hostEnd - hostStart);

        var thirdParty = !Domains.SameSite(host.ToString(), docHost);

        var block = Find(_blockHosts, _blockTokens, _blockGeneric, lower, host, hostStart, hostEnd, docHost, type, thirdParty, _hasImportant);
        if (block == null) return null;
        if (block.Important) return block;
        var allow = Find(_allowHosts, _allowTokens, _allowGeneric, lower, host, hostStart, hostEnd, docHost, type, thirdParty, false);
        return allow == null ? block : null;
    }

    /// <param name="keepLookingForImportant">If true, a normal match doesn't end the search (an $important rule may still match).</param>
    private static NetRule? Find(Dictionary<ulong, List<NetRule>> hosts, Dictionary<ulong, List<NetRule>> tokens, List<NetRule> generic,
        string url, ReadOnlySpan<char> host, int hostStart, int hostEnd, string docHost, ResourceType type, bool thirdParty,
        bool keepLookingForImportant)
    {
        NetRule? found = null;

        // 1) Hostname table: "ads.example.com" checks "ads.example.com", "example.com", "com".
        for (var h = host; ; )
        {
            if (hosts.TryGetValue(Hash(h), out var list))
                foreach (var r in list)
                    if (h.SequenceEqual(r.Host) && r.MatchesContext(type, thirdParty, docHost))
                    {
                        if (r.Important || !keepLookingForImportant) return r;
                        found ??= r;
                    }
            var dot = h.IndexOf('.');
            if (dot < 0) break;
            h = h[(dot + 1)..];
        }

        // 2) Token index: every whole word of the URL.
        var span = url.AsSpan();
        int i = 0;
        while (i < span.Length)
        {
            if (!IsTokenChar(span[i])) { i++; continue; }
            var start = i;
            while (i < span.Length && IsTokenChar(span[i])) i++;
            if (i - start < 2) continue;
            if (!tokens.TryGetValue(Hash(span[start..i]), out var list)) continue;
            foreach (var r in list)
            {
                if (found != null && !r.Important) continue;
                if (r.MatchesContext(type, thirdParty, docHost) && r.MatchesUrl(url, hostStart, hostEnd))
                {
                    if (r.Important || !keepLookingForImportant) return r;
                    found ??= r;
                }
            }
        }

        // 3) Rules without a usable word.
        if (found == null)
            foreach (var r in generic)
                if (r.MatchesContext(type, thirdParty, docHost) && r.MatchesUrl(url, hostStart, hostEnd)) return r;

        return found;
    }

    /// <summary>
    /// CSS for the start of a page: the site-specific rules plus the generic selectors that don't depend
    /// on a class/id. Empty if element hiding is off for this site.
    /// </summary>
    public string CosmeticCss(string docHost)
    {
        if (SiteIn(_elemHideSites, docHost)) return "";
        var unhide = Unhidden(docHost);
        var selectors = new List<string>();
        for (var h = docHost; ; )
        {
            if (_hide.TryGetValue(h, out var list)) selectors.AddRange(list);
            var dot = h.IndexOf('.');
            if (dot < 0) break;
            h = h[(dot + 1)..];
        }
        if (!SiteIn(_genericHideSites, docHost)) selectors.AddRange(_highGeneric);
        return ToCss(selectors.Where(s => !unhide.Contains(s)).Distinct());
    }

    /// <summary>
    /// Generic selectors whose leading class/id appears in the page (tokens like ".ad-box" or "#banner"),
    /// skipping the ones in <paramref name="alreadySent"/> (which is updated).
    /// </summary>
    public string GenericCssFor(IEnumerable<string> pageTokens, string docHost, HashSet<string> alreadySent)
    {
        if (SiteIn(_elemHideSites, docHost) || SiteIn(_genericHideSites, docHost)) return "";
        HashSet<string>? unhide = null;
        var selectors = new List<string>();
        foreach (var token in pageTokens)
        {
            if (!_genericByToken.TryGetValue(token, out var list)) continue;
            unhide ??= Unhidden(docHost);
            foreach (var s in list)
                if (!unhide.Contains(s) && alreadySent.Add(s)) selectors.Add(s);
        }
        return ToCss(selectors);
    }

    private HashSet<string> Unhidden(string docHost)
    {
        var unhide = new HashSet<string>(StringComparer.Ordinal);
        for (var h = docHost; ; )
        {
            if (_unhide.TryGetValue(h, out var set)) unhide.UnionWith(set);
            var dot = h.IndexOf('.');
            if (dot < 0) return unhide;
            h = h[(dot + 1)..];
        }
    }

    // One rule per selector, so one invalid selector can't disable the others.
    private static string ToCss(IEnumerable<string> selectors) =>
        string.Concat(selectors.Select(s => s + "{display:none!important}\n"));
}
