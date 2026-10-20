using Phast.Adblock;
using Xunit;

namespace Phast.Tests;

public class FilterEngineTests
{
    private static FilterEngine Engine(params string[] rules)
    {
        var e = new FilterEngine();
        e.AddList(rules);
        return e;
    }

    private static bool Blocks(FilterEngine e, string url, string page = "news.example.com", ResourceType type = ResourceType.Script) =>
        e.Match(url, page, type) != null;

    [Fact]
    public void HostRule_BlocksDomainAndSubdomains()
    {
        var e = Engine("||doubleclick.net^");
        Assert.True(Blocks(e, "https://doubleclick.net/x.js"));
        Assert.True(Blocks(e, "https://ad.g.doubleclick.net/pagead/ads?x=1"));
        Assert.False(Blocks(e, "https://notdoubleclick.net/x.js"));
        Assert.False(Blocks(e, "https://doubleclick.network/x.js"));
    }

    [Fact]
    public void Exception_Wins_UnlessImportant()
    {
        var e = Engine("||ads.example.org^", "@@||ads.example.org/allowed/");
        Assert.True(Blocks(e, "https://ads.example.org/banner.js"));
        Assert.False(Blocks(e, "https://ads.example.org/allowed/thing.js"));

        var important = Engine("||ads.example.org^$important", "@@||ads.example.org/allowed/");
        Assert.True(Blocks(important, "https://ads.example.org/allowed/thing.js"));
    }

    [Fact]
    public void PathPatterns_WithWildcardsAndSeparators()
    {
        var e = Engine("/banner/*/ad_", "-ad-300x250.", "&ad_type=");
        Assert.True(Blocks(e, "https://cdn.site.com/banner/2024/ad_top.png", type: ResourceType.Image));
        Assert.True(Blocks(e, "https://cdn.site.com/img/top-ad-300x250.gif", type: ResourceType.Image));
        Assert.True(Blocks(e, "https://x.com/serve?id=1&ad_type=video", type: ResourceType.Xhr));
        Assert.False(Blocks(e, "https://cdn.site.com/banner/logo.png", type: ResourceType.Image));
    }

    [Fact]
    public void Separator_DoesNotMatchLettersOrDots()
    {
        var e = Engine("||tracker.com^track");
        Assert.True(Blocks(e, "https://tracker.com/track"));
        Assert.False(Blocks(e, "https://tracker.community/track"));
    }

    [Fact]
    public void ThirdPartyOption()
    {
        var e = Engine("||cdn-ads.net^$third-party");
        Assert.True(Blocks(e, "https://cdn-ads.net/a.js", page: "www.periodico.es"));
        Assert.False(Blocks(e, "https://cdn-ads.net/a.js", page: "www.cdn-ads.net"));
    }

    [Fact]
    public void TypeOptions_AndNegation()
    {
        var e = Engine("||stats.io^$script", "||pixel.io^$~image");
        Assert.True(Blocks(e, "https://stats.io/s.js", type: ResourceType.Script));
        Assert.False(Blocks(e, "https://stats.io/p.gif", type: ResourceType.Image));
        Assert.True(Blocks(e, "https://pixel.io/x", type: ResourceType.Xhr));
        Assert.False(Blocks(e, "https://pixel.io/x.gif", type: ResourceType.Image));
    }

    [Fact]
    public void DomainOption()
    {
        var e = Engine("/promo.js$domain=a.com|~shop.a.com");
        Assert.True(Blocks(e, "https://cdn.x.net/promo.js", page: "www.a.com"));
        Assert.False(Blocks(e, "https://cdn.x.net/promo.js", page: "shop.a.com"));
        Assert.False(Blocks(e, "https://cdn.x.net/promo.js", page: "b.com"));
    }

    [Fact]
    public void UnsupportedRules_AreIgnored_NotMisapplied()
    {
        // redirect / csp / regex rules must not turn into "block everything".
        var e = Engine("||good.com^$redirect=noop.js", "/^https?:\\/\\/[a-z]+\\.com\\//", "$csp=script-src 'none'", "*", "|https:");
        Assert.False(Blocks(e, "https://good.com/app.js"));
        Assert.False(Blocks(e, "https://example.com/"));
        Assert.Equal(0, e.NetworkRuleCount);
    }

    [Fact]
    public void DocumentException_DisablesSite()
    {
        var e = Engine("@@||trusted.org^$document");
        Assert.True(e.IsSiteExempt("www.trusted.org"));
        Assert.False(e.IsSiteExempt("untrusted.org"));
    }

    [Fact]
    public void Cosmetic_SiteSpecificOnly()
    {
        var e = Engine("marca.com##.ad-banner", "marca.com#@#.ad-banner", "marca.com##.publi", "##.generic-ad", "elpais.com##+js(noop)");
        var css = e.CosmeticCss("www.marca.com");
        Assert.Contains(".publi{display:none!important}", css);
        Assert.DoesNotContain(".ad-banner", css);     // exception
        Assert.DoesNotContain(".generic-ad", css);    // generic rules are not used
        Assert.Equal("", e.CosmeticCss("www.elpais.com"));
    }

    [Fact]
    public void Cosmetic_Generic_OnlyForTokensInPage()
    {
        var e = Engine("##.ad-box", "##.ad-box > .inner", "###top-banner", "##[data-ad-slot]", "example.org#@#.ad-box");
        var sent = new HashSet<string>();

        // Selectors without a class/id are part of the base CSS of every page.
        Assert.Contains("[data-ad-slot]{display:none!important}", e.CosmeticCss("news.com"));

        var css = e.GenericCssFor([".ad-box", ".other"], "news.com", sent);
        Assert.Contains(".ad-box{display:none!important}", css);
        Assert.Contains(".ad-box > .inner{display:none!important}", css);
        Assert.DoesNotContain("#top-banner", css);

        // Already sent selectors are not sent twice; new tokens bring their own.
        var more = e.GenericCssFor([".ad-box", "#top-banner"], "news.com", sent);
        Assert.Equal("#top-banner{display:none!important}\n", more);

        // Site-specific exception.
        Assert.DoesNotContain(".ad-box{", e.GenericCssFor([".ad-box"], "www.example.org", new HashSet<string>()));
    }

    [Fact]
    public void GenericHideException_DisablesGenericHidingOnly_AndAllowsNoRequests()
    {
        var e = Engine("##.ad-box", "##[data-ad]", "shop.com##.promo", "||ads.net^", "@@||shop.com^$generichide");
        Assert.Equal("", e.GenericCssFor([".ad-box"], "www.shop.com", new HashSet<string>()));
        var baseCss = e.CosmeticCss("www.shop.com");
        Assert.Contains(".promo", baseCss);            // site-specific rules still apply
        Assert.DoesNotContain("[data-ad]", baseCss);   // generic ones don't
        Assert.True(Blocks(e, "https://ads.net/x.js", page: "www.shop.com")); // network blocking unaffected
    }

    [Theory]
    [InlineData("www.bbc.co.uk", "bbc.co.uk")]
    [InlineData("a.b.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("192.168.1.10", "192.168.1.10")]
    public void RegistrableDomain(string host, string expected) =>
        Assert.Equal(expected, Domains.RegistrableDomain(host));
}
