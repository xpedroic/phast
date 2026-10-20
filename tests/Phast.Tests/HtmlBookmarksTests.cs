using Phast.Bookmarks;
using Xunit;

namespace Phast.Tests;

public class HtmlBookmarksTests
{
    // Shape of a file exported by Chrome (Netscape bookmark format).
    private const string ChromeExport = """
        <!DOCTYPE NETSCAPE-Bookmark-file-1>
        <META HTTP-EQUIV="Content-Type" CONTENT="text/html; charset=UTF-8">
        <TITLE>Bookmarks</TITLE>
        <H1>Bookmarks</H1>
        <DL><p>
            <DT><H3 ADD_DATE="1" LAST_MODIFIED="2" PERSONAL_TOOLBAR_FOLDER="true">Barra de marcadores</H3>
            <DL><p>
                <DT><A HREF="https://github.com/" ADD_DATE="1">GitHub</A>
                <DT><H3 ADD_DATE="1">Trabajo &amp; ocio</H3>
                <DL><p>
                    <DT><A HREF="https://example.org/a?x=1&amp;y=2" ADD_DATE="1">Ejemplo “raro” &lt;3</A>
                </DL><p>
            </DL><p>
            <DT><A HREF="https://duckduckgo.com/" ADD_DATE="1">DuckDuckGo</A>
            <DT><A HREF="javascript:alert(1)">Bookmarklet</A>
        </DL><p>
        """;

    [Fact]
    public void Parse_ChromeExport()
    {
        var root = HtmlBookmarks.Parse(ChromeExport, "root", out var toolbar);

        Assert.NotNull(toolbar);
        Assert.Equal("Barra de marcadores", toolbar!.Title);
        Assert.Equal(2, toolbar.Children!.Count);
        Assert.Equal("GitHub", toolbar.Children[0].Title);

        var folder = toolbar.Children[1];
        Assert.True(folder.IsFolder);
        Assert.Equal("Trabajo & ocio", folder.Title);
        Assert.Equal("https://example.org/a?x=1&y=2", folder.Children![0].Url);
        Assert.Equal("Ejemplo “raro” <3", folder.Children[0].Title);

        // Loose bookmark next to the toolbar folder; bookmarklets are skipped.
        Assert.Contains(root.Children!, n => n.Url == "https://duckduckgo.com/");
        Assert.DoesNotContain(root.Descendants(), n => n.Url?.StartsWith("javascript:") == true);
    }

    [Fact]
    public void Export_ThenParse_RoundTrips()
    {
        var data = new BookmarkData();
        data.Bar.Children!.Add(BookmarkNode.Link("Phast <repo>", "https://github.com/xpedroic/phast?a=1&b=2"));
        var folder = BookmarkNode.Folder("Ocio");
        folder.Children!.Add(BookmarkNode.Link("Twitch", "https://www.twitch.tv/"));
        data.Bar.Children.Add(folder);
        data.Other.Children!.Add(BookmarkNode.Link("Otro", "https://example.com/"));

        var html = HtmlBookmarks.Export(data);
        var root = HtmlBookmarks.Parse(html, "root", out var toolbar);

        Assert.NotNull(toolbar);
        Assert.Equal("Phast <repo>", toolbar!.Children![0].Title);
        Assert.Equal("https://github.com/xpedroic/phast?a=1&b=2", toolbar.Children[0].Url);
        Assert.Equal("Twitch", toolbar.Children[1].Children![0].Title);
        Assert.Contains(root.Descendants(), n => n.Url == "https://example.com/");
    }
}
