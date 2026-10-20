namespace Phast.Core;

/// <summary>
/// A search engine. <see cref="SuggestUrl"/> must return OpenSearch JSON: ["query", ["suggestion1", ...]].
/// </summary>
public sealed record SearchEngine(string Id, string Name, string SearchUrl, string? SuggestUrl)
{
    public string BuildUrl(string query) => SearchUrl.Replace("{query}", Uri.EscapeDataString(query));
    public string? BuildSuggestUrl(string query) => SuggestUrl?.Replace("{query}", Uri.EscapeDataString(query));
}

/// <summary>Built-in search engines the user can choose from in Settings.</summary>
public static class SearchEngines
{
    public static readonly IReadOnlyList<SearchEngine> All =
    [
        new("google", "Google", "https://www.google.com/search?q={query}",
            "https://suggestqueries.google.com/complete/search?client=firefox&ie=utf-8&oe=utf-8&q={query}"),
        new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q={query}",
            "https://duckduckgo.com/ac/?q={query}&type=list"),
        new("brave", "Brave Search", "https://search.brave.com/search?q={query}",
            "https://search.brave.com/api/suggest?q={query}"),
        new("bing", "Bing", "https://www.bing.com/search?q={query}",
            "https://api.bing.com/osjson.aspx?query={query}"),
        new("startpage", "Startpage", "https://www.startpage.com/do/search?q={query}",
            "https://www.startpage.com/osuggestions?q={query}"),
    ];

    public static SearchEngine Get(string id) => All.FirstOrDefault(e => e.Id == id) ?? All[0];
}
