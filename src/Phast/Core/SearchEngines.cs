namespace Phast.Core;

public sealed record SearchEngine(string Id, string Name, string SearchUrl)
{
    public string BuildUrl(string query) => SearchUrl.Replace("{query}", Uri.EscapeDataString(query));
}

/// <summary>Built-in search engines the user can choose from in Settings.</summary>
public static class SearchEngines
{
    public static readonly IReadOnlyList<SearchEngine> All =
    [
        new("google", "Google", "https://www.google.com/search?q={query}"),
        new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com/?q={query}"),
        new("brave", "Brave Search", "https://search.brave.com/search?q={query}"),
        new("bing", "Bing", "https://www.bing.com/search?q={query}"),
        new("startpage", "Startpage", "https://www.startpage.com/do/search?q={query}"),
    ];

    public static SearchEngine Get(string id) => All.FirstOrDefault(e => e.Id == id) ?? All[0];
}
