using Phast.Core;
using Phast.History;
using Phast.Settings;

namespace Phast.Omnibox;

/// <summary>Result of the local (instant) suggestion pass.</summary>
public sealed record LocalSuggestions(List<Suggestion> Rows, string? InlineCompletion);

/// <summary>
/// Builds the address bar dropdown: the "what you typed" row, matching history (and bookmarks),
/// and search suggestions, plus inline autocompletion of addresses ("gi" -> "github.com").
/// </summary>
public static class SuggestionProvider
{
    private const int MaxHistoryRows = 4;
    private const int MaxSearchRows = 4;

    /// <summary>Extra local source (bookmarks) plugged in by the bookmarks feature.</summary>
    public static Func<string, IEnumerable<Suggestion>>? BookmarkSource { get; set; }

    public static async Task<LocalSuggestions> GetLocalAsync(string typed)
    {
        typed = typed.Trim();
        var rows = new List<Suggestion>();
        if (typed.Length == 0) return new LocalSuggestions(rows, null);

        var history = await HistoryStore.SearchAsync(typed).ConfigureAwait(true);
        var ranked = history
            .Select(h => (Entry: h, Score: Score(h, typed)))
            .OrderByDescending(x => x.Score)
            .ToList();

        // Inline autocomplete only for address-like input that is a prefix of a well-known URL.
        string? completion = null;
        Suggestion? defaultRow = null;
        if (!typed.Contains(' '))
        {
            var best = ranked.Select(x => x.Entry)
                .Where(h => (h.TypedCount > 0 || h.VisitCount >= 2) && Strip(h.Url).StartsWith(typed, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(h => h.TypedCount * 5 + h.VisitCount)
                .FirstOrDefault();
            var stripped = best != null ? Strip(best.Url) : "";
            var host = stripped.Split('/')[0];
            // Complete to the site first ("github.com"), the full address only when the user is typing a path.
            var fill = typed.Length < host.Length ? host : stripped.TrimEnd('/');
            if (best != null && fill.Length > typed.Length && fill.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            {
                completion = typed + fill[typed.Length..];
                var target = Prefix(best.Url) + fill;
                defaultRow = new Suggestion
                {
                    Kind = SuggestionKind.Url,
                    Text = completion,
                    Detail = fill == stripped.TrimEnd('/') && best.Title.Length > 0 ? best.Title : null,
                    Target = target,
                    FillText = completion,
                };
            }
        }

        rows.Add(defaultRow ?? WhatYouTyped(typed));

        // Bookmarks first (they are explicit user choices), then history.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Normalize(rows[0].Target) };
        if (BookmarkSource != null)
        {
            foreach (var b in BookmarkSource(typed).Take(3))
                if (seen.Add(Normalize(b.Target))) rows.Add(b);
        }
        foreach (var (h, _) in ranked)
        {
            if (rows.Count >= 1 + MaxHistoryRows) break;
            if (!seen.Add(Normalize(h.Url))) continue;
            rows.Add(new Suggestion
            {
                Kind = SuggestionKind.History,
                Text = string.IsNullOrWhiteSpace(h.Title) ? Strip(h.Url) : h.Title,
                Detail = Strip(h.Url).TrimEnd('/'),
                Target = h.Url,
                FillText = h.Url,
            });
        }

        return new LocalSuggestions(rows, completion);
    }

    /// <summary>Search suggestions from the engine, as dropdown rows (empty if disabled or not applicable).</summary>
    public static async Task<List<Suggestion>> GetSearchAsync(string typed, CancellationToken ct)
    {
        typed = typed.Trim();
        if (!SettingsService.Current.SearchSuggestions || typed.Length == 0 || LooksPrivate(typed)) return [];
        var engine = SearchEngines.Get(SettingsService.Current.SearchEngine);
        var results = await SearchSuggestClient.GetAsync(engine, typed, ct).ConfigureAwait(true);
        return results
            .Where(s => !s.Equals(typed, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSearchRows)
            .Select(s => new Suggestion
            {
                Kind = SuggestionKind.Search,
                Text = s,
                Target = engine.BuildUrl(s),
                FillText = s,
            })
            .ToList();
    }

    /// <summary>First row when nothing is autocompleted: "go to address" or "search for ...".</summary>
    private static Suggestion WhatYouTyped(string typed)
    {
        var engine = SearchEngines.Get(SettingsService.Current.SearchEngine);
        var url = UrlResolver.Resolve(typed) ?? engine.BuildUrl(typed);
        var isSearch = url == engine.BuildUrl(typed);
        return new Suggestion
        {
            Kind = isSearch ? SuggestionKind.Search : SuggestionKind.Url,
            Text = typed,
            Detail = isSearch ? $"Búsqueda de {engine.Name}" : null,
            Target = url,
            FillText = typed,
        };
    }

    /// <summary>Relevance of a history entry for the typed text (higher is better).</summary>
    private static double Score(HistoryEntry h, string typed)
    {
        var stripped = Strip(h.Url);
        double score;
        if (stripped.StartsWith(typed, StringComparison.OrdinalIgnoreCase))
            score = 1000 + (stripped.Split('/')[0].Length + 1 >= stripped.TrimEnd('/').Length ? 200 : 0); // prefer site roots
        else if (stripped.Contains("." + typed, StringComparison.OrdinalIgnoreCase) || stripped.Contains("/" + typed, StringComparison.OrdinalIgnoreCase))
            score = 500;
        else if (h.Title.StartsWith(typed, StringComparison.OrdinalIgnoreCase) || h.Title.Contains(" " + typed, StringComparison.OrdinalIgnoreCase))
            score = 400;
        else
            score = 100;

        var days = (DateTime.Now - h.LastVisit).TotalDays;
        score += Math.Log(h.VisitCount + 1) * 60 + h.TypedCount * 40 + Math.Max(0, 120 - days * 4);
        return score;
    }

    /// <summary>Don't send local addresses or file paths to a search engine.</summary>
    private static bool LooksPrivate(string text) =>
        text.Contains("://") || text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
        || (text.Length > 2 && text[1] == ':') || text.StartsWith("\\\\");

    /// <summary>"https://www.github.com/x" -> "github.com/x"</summary>
    public static string Strip(string url)
    {
        var s = url;
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];
        if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        return s;
    }

    /// <summary>"https://www.github.com/x" -> "https://www."</summary>
    private static string Prefix(string url) => url[..(url.Length - Strip(url).Length)];

    private static string Normalize(string url) => Strip(url).TrimEnd('/');
}
