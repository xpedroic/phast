using Phast.Core;
using Phast.Omnibox;
using Xunit;

namespace Phast.Tests;

public class UrlTests
{
    [Theory]
    [InlineData("github.com", "https://github.com")]
    [InlineData("github.com/xpedroic/phast", "https://github.com/xpedroic/phast")]
    [InlineData("https://example.org/a?b=1", "https://example.org/a?b=1")]
    [InlineData("localhost:3000", "http://localhost:3000")]
    [InlineData("192.168.1.1", "http://192.168.1.1")]
    [InlineData("about:blank", "about:blank")]
    [InlineData("phast://settings", "https://phast.internal/settings.html")]
    [InlineData("phast://settings#bookmarks", "https://phast.internal/settings.html#bookmarks")]
    public void Resolve_Addresses(string input, string expected) =>
        Assert.Equal(expected, UrlResolver.Resolve(input));

    [Theory]
    [InlineData("receta tortilla")]
    [InlineData("hola")]
    [InlineData("¿qué hora es?")]
    public void Resolve_Searches(string input) =>
        Assert.StartsWith("https://www.google.com/search?q=", UrlResolver.Resolve(input));

    [Fact]
    public void Resolve_Empty_IsNull() => Assert.Null(UrlResolver.Resolve("   "));

    [Theory]
    [InlineData("https://phast.internal/settings.html#search", "phast://settings#search")]
    [InlineData("https://phast.internal/bookmarks.html", "phast://bookmarks")]
    [InlineData("https://github.com/", "https://github.com/")]
    public void InternalPages_DisplayUrl(string real, string display) =>
        Assert.Equal(display, InternalPages.ToDisplayUrl(real));

    [Theory]
    [InlineData("https://www.github.com/x", "github.com/x")]
    [InlineData("http://example.org/", "example.org/")]
    public void Strip_RemovesSchemeAndWww(string url, string expected) =>
        Assert.Equal(expected, SuggestionProvider.Strip(url));
}
