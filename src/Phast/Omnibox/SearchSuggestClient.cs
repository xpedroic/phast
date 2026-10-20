using System.Net.Http;
using System.Text.Json;
using Phast.Core;

namespace Phast.Omnibox;

/// <summary>
/// Fetches search suggestions ("did you mean...") from the selected search engine.
/// Only used while typing in the address bar and only if enabled in Settings.
/// </summary>
public static class SearchSuggestClient
{
    private static HttpClient? _http;

    private static HttpClient Http => _http ??= CreateClient();

    private static HttpClient CreateClient()
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        })
        {
            Timeout = TimeSpan.FromSeconds(2),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Phast");
        return http;
    }

    public static async Task<IReadOnlyList<string>> GetAsync(SearchEngine engine, string query, CancellationToken ct)
    {
        var url = engine.BuildSuggestUrl(query);
        if (url == null) return [];
        try
        {
            using var response = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return [];
            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // OpenSearch format: ["query", ["suggestion 1", "suggestion 2", ...], ...]
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2) return [];
            return doc.RootElement[1].EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or OperationCanceledException)
        {
            return [];
        }
    }
}
