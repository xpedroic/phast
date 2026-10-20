namespace Phast.Adblock;

/// <summary>
/// Approximates "same site" (registrable domain, eTLD+1) without shipping the full Public Suffix List:
/// "a.example.co.uk" and "b.example.co.uk" are the same site, "example.com" and "cdn.other.com" are not.
/// </summary>
public static class Domains
{
    // Common two-part public suffixes (second-level registries).
    private static readonly HashSet<string> SecondLevel =
    [
        "co.uk", "org.uk", "ac.uk", "gov.uk", "me.uk", "ltd.uk", "plc.uk",
        "com.es", "org.es", "gob.es", "nom.es", "edu.es",
        "com.mx", "org.mx", "gob.mx", "com.ar", "gob.ar", "com.co", "com.pe", "com.ve", "com.uy", "com.ec", "cl.cl",
        "com.br", "org.br", "gov.br", "com.au", "net.au", "org.au", "co.nz", "co.jp", "ne.jp", "or.jp",
        "co.in", "co.za", "com.tr", "com.cn", "com.hk", "com.tw", "co.kr", "com.sg", "com.my",
        "github.io", "blogspot.com", "herokuapp.com", "netlify.app", "vercel.app", "pages.dev", "web.app", "firebaseapp.com",
        "azurewebsites.net", "cloudfront.net", "appspot.com",
    ];

    public static string RegistrableDomain(string host)
    {
        if (host.Length == 0 || char.IsAsciiDigit(host[^1]) || host.Contains(':')) return host; // IP addresses
        var last = host.LastIndexOf('.');
        if (last < 0) return host;
        var second = host.LastIndexOf('.', last - 1);
        if (second < 0) return host;
        var lastTwo = host[(second + 1)..];
        if (!SecondLevel.Contains(lastTwo)) return lastTwo;
        var third = host.LastIndexOf('.', second - 1);
        return third < 0 ? host : host[(third + 1)..];
    }

    public static bool SameSite(string a, string b) =>
        a.Length > 0 && b.Length > 0 && string.Equals(RegistrableDomain(a), RegistrableDomain(b), StringComparison.Ordinal);
}
