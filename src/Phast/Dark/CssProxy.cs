using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Phast.Dark;

/// <summary>
/// Lets Dark Reader read stylesheets and images from other sites (which a page normally can't, because of CORS).
/// Strictly limited: only public http(s) addresses, only CSS or images, no cookies, size and time limits.
/// </summary>
public static class CssProxy
{
    private const long MaxBytes = 4 * 1024 * 1024;

    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var http = new HttpClient(new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            MaxAutomaticRedirections = 5,
        })
        {
            Timeout = TimeSpan.FromSeconds(10),
            MaxResponseContentBufferSize = MaxBytes,
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36");
        return http;
    });

    /// <summary>Never fetch from this computer or the local network (a web page must not use us to reach them).</summary>
    public static bool IsSafeRemote(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https")) return false;
        var host = uri.Host.ToLowerInvariant();
        if (host is "localhost" || host.EndsWith(".localhost") || host.EndsWith(".local") || host.EndsWith(".internal")
            || host.EndsWith(".lan") || host.EndsWith(".home") || !host.Contains('.'))
            return false;
        if (IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return !IsPrivate(ip);
        return true;
    }

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal || ip.Equals(IPAddress.IPv6Any);
        var b = ip.GetAddressBytes();
        return b[0] is 10 or 127 or 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168)
               || (b[0] == 169 && b[1] == 254) || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }

    /// <summary>Downloads a stylesheet or image. Returns (bytes, content type) or null if not allowed / failed.</summary>
    public static async Task<(byte[] Data, string ContentType)?> FetchAsync(Uri uri)
    {
        if (!IsSafeRemote(uri)) return null;
        try
        {
            using var response = await Http.Value.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode || response.RequestMessage?.RequestUri is { } final && !IsSafeRemote(final)) return null;
            var type = response.Content.Headers.ContentType?.MediaType ?? "";
            var path = uri.AbsolutePath.ToLowerInvariant();
            var isCss = type.Contains("css") || (type is "text/plain" or "" && path.EndsWith(".css"));
            var isImage = type.StartsWith("image/");
            if (!isCss && !isImage) return null;
            if (response.Content.Headers.ContentLength > MaxBytes) return null;
            var data = await response.Content.ReadAsByteArrayAsync();
            return (data, isCss ? "text/css; charset=utf-8" : type);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }
}
