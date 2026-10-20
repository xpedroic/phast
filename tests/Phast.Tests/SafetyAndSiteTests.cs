using Phast.Adblock;
using Phast.Core;
using Phast.Dark;
using Phast.Settings;
using Phast.Tabs;
using Xunit;

namespace Phast.Tests;

/// <summary>Security-sensitive checks and per-site preferences.</summary>
public class SafetyAndSiteTests
{
    [Theory]
    [InlineData("https://cdn.example.com/style.css", true)]
    [InlineData("http://203.0.113.10/a.css", true)]
    [InlineData("http://localhost/a.css", false)]
    [InlineData("http://127.0.0.1/a.css", false)]
    [InlineData("http://192.168.1.1/admin", false)]
    [InlineData("http://10.0.0.5/x.css", false)]
    [InlineData("http://172.20.1.1/x.css", false)]
    [InlineData("http://169.254.169.254/latest/meta-data", false)]
    [InlineData("http://router.local/x.css", false)]
    [InlineData("http://intranet/x.css", false)]
    [InlineData("http://[::1]/x.css", false)]
    [InlineData("file:///C:/Windows/win.ini", false)]
    public void DarkModeStyleProxy_NeverReachesLocalNetwork(string url, bool allowed) =>
        Assert.Equal(allowed, CssProxy.IsSafeRemote(new Uri(url)));

    [Fact]
    public void DarkMode_PerSite_AndAllSitesWithExceptions()
    {
        SettingsService.ReplaceAll(new AppSettings());
        Assert.False(DarkModeService.IsWanted("es.wikipedia.org"));
        Assert.True(DarkModeService.Toggle("es.wikipedia.org"));
        Assert.True(DarkModeService.IsWanted("en.wikipedia.org"));    // remembered for the whole site
        Assert.False(DarkModeService.IsWanted("github.com"));

        SettingsService.Update(s => s.DarkAllSites = true);
        Assert.True(DarkModeService.IsWanted("github.com"));
        Assert.False(DarkModeService.Toggle("github.com"));           // excluded from "all sites"
        Assert.False(DarkModeService.IsWanted("github.com"));
        Assert.False(DarkModeService.IsWanted(InternalPages.Host));   // Phast's own pages follow the theme instead
        SettingsService.ReplaceAll(new AppSettings());
    }

    [Fact]
    public void SiteOverrides_ExactHostWinsOverSite()
    {
        SettingsService.ReplaceAll(new AppSettings());
        SettingsService.Update(s =>
        {
            s.SiteOverrides["example.com"] = new SiteOverride { JavaScriptDisabled = true };
            s.SiteOverrides["mail.example.com"] = new SiteOverride { UserAgent = "Test" };
        });
        Assert.True(BrowserTab.OverrideFor("www.example.com")!.JavaScriptDisabled);
        Assert.Equal("Test", BrowserTab.OverrideFor("mail.example.com")!.UserAgent);
        Assert.Null(BrowserTab.OverrideFor("other.org"));
        SettingsService.ReplaceAll(new AppSettings());
    }

    [Fact]
    public void Adblock_SiteSwitch_AndCustomRulesAndLists()
    {
        SettingsService.ReplaceAll(new AppSettings());
        AdblockService.SetAllowed("www.marca.com", allowed: true);
        Assert.Contains("marca.com", SettingsService.Current.AdblockAllowlist);
        Assert.False(AdblockService.IsActiveFor("www.marca.com"));
        AdblockService.SetAllowed("www.marca.com", allowed: false);
        Assert.True(AdblockService.IsActiveFor("www.marca.com"));

        // Custom list ids are stable, so their downloaded copy is found again after a restart.
        Assert.Equal(FilterLists.CustomId("https://x.example/list.txt"), FilterLists.CustomId(" https://x.example/list.txt "));
        SettingsService.ReplaceAll(new AppSettings());
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=1", "www.youtube.com")]
    [InlineData("http://example.org/", "example.org")]
    [InlineData("phast://settings", null)]
    [InlineData("not a url", null)]
    public void FaviconCache_HostOf(string url, string? host) => Assert.Equal(host, FaviconCache.HostOf(url));

    [Fact]
    public void Search_UsesTheChosenEngine()
    {
        SettingsService.ReplaceAll(new AppSettings());
        SettingsService.Update(s => s.SearchEngine = "duckduckgo");
        Assert.StartsWith("https://duckduckgo.com/?q=hola", UrlResolver.Resolve("hola"));
        SettingsService.ReplaceAll(new AppSettings());
    }
}
