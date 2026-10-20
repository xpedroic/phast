using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Phast.Bookmarks;

/// <summary>
/// Reads and writes the "Netscape bookmark file" HTML format that every browser can export/import.
///   &lt;DT&gt;&lt;H3&gt;Folder&lt;/H3&gt; &lt;DL&gt;&lt;p&gt; ... &lt;/DL&gt;&lt;p&gt;
///   &lt;DT&gt;&lt;A HREF="https://..." ADD_DATE="..."&gt;Title&lt;/A&gt;
/// </summary>
public static partial class HtmlBookmarks
{
    [GeneratedRegex(@"<(?<close>/)?(?<tag>DL|H3|A)\b(?<attrs>[^>]*)>(?:(?<text>[^<]*)</(?:H3|A)>)?", RegexOptions.IgnoreCase)]
    private static partial Regex Token();

    [GeneratedRegex(@"HREF\s*=\s*""(?<url>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    [GeneratedRegex(@"PERSONAL_TOOLBAR_FOLDER\s*=\s*""true""", RegexOptions.IgnoreCase)]
    private static partial Regex ToolbarFolder();

    /// <summary>Parses an exported bookmarks file into a folder named <paramref name="rootTitle"/>.</summary>
    public static BookmarkNode Parse(string html, string rootTitle, out BookmarkNode? toolbar)
    {
        toolbar = null;
        var root = BookmarkNode.Folder(rootTitle);
        var stack = new Stack<BookmarkNode>();
        stack.Push(root);
        BookmarkNode? pendingFolder = null;

        foreach (Match m in Token().Matches(html))
        {
            var tag = m.Groups["tag"].Value.ToUpperInvariant();
            var closing = m.Groups["close"].Success;
            switch (tag)
            {
                case "H3":
                    pendingFolder = BookmarkNode.Folder(WebUtility.HtmlDecode(m.Groups["text"].Value.Trim()));
                    stack.Peek().Children!.Add(pendingFolder);
                    if (ToolbarFolder().IsMatch(m.Groups["attrs"].Value)) toolbar = pendingFolder;
                    break;
                case "DL" when !closing:
                    // The <DL> right after an <H3> holds that folder's content.
                    if (pendingFolder != null) { stack.Push(pendingFolder); pendingFolder = null; }
                    break;
                case "DL" when closing:
                    if (stack.Count > 1) stack.Pop();
                    break;
                case "A":
                    var href = Href().Match(m.Groups["attrs"].Value);
                    if (!href.Success) break;
                    var url = WebUtility.HtmlDecode(href.Groups["url"].Value);
                    if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("place:", StringComparison.OrdinalIgnoreCase)) break;
                    stack.Peek().Children!.Add(BookmarkNode.Link(WebUtility.HtmlDecode(m.Groups["text"].Value.Trim()), url));
                    break;
            }
        }
        return root;
    }

    /// <summary>Exports all bookmarks in the standard format (importable by Chrome, Firefox, Edge...).</summary>
    public static string Export(BookmarkData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        sb.AppendLine("<!-- This is an automatically generated file. It will be read and overwritten. DO NOT EDIT! -->");
        sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        sb.AppendLine("<TITLE>Bookmarks</TITLE>");
        sb.AppendLine("<H1>Bookmarks</H1>");
        sb.AppendLine("<DL><p>");
        WriteFolder(sb, data.Bar, 1, toolbar: true);
        WriteFolder(sb, data.Other, 1, toolbar: false);
        sb.AppendLine("</DL><p>");
        return sb.ToString();
    }

    private static void WriteFolder(StringBuilder sb, BookmarkNode folder, int depth, bool toolbar = false)
    {
        var pad = new string(' ', depth * 4);
        var added = folder.Added / 1000;
        sb.AppendLine($"{pad}<DT><H3 ADD_DATE=\"{added}\"{(toolbar ? " PERSONAL_TOOLBAR_FOLDER=\"true\"" : "")}>{WebUtility.HtmlEncode(folder.Title)}</H3>");
        sb.AppendLine($"{pad}<DL><p>");
        foreach (var c in folder.Children!)
        {
            if (c.IsFolder) WriteFolder(sb, c, depth + 1);
            else sb.AppendLine($"{pad}    <DT><A HREF=\"{WebUtility.HtmlEncode(c.Url)}\" ADD_DATE=\"{c.Added / 1000}\">{WebUtility.HtmlEncode(c.Title)}</A>");
        }
        sb.AppendLine($"{pad}</DL><p>");
    }

    /// <summary>Imports a parsed file: the toolbar folder (if any) goes to the bar when it's empty, the rest to "Otros marcadores".</summary>
    public static int Import(string html)
    {
        var root = Parse(html, "Importado (HTML)", out var toolbar);
        var count = root.Descendants().Count(n => n.Url != null);
        if (toolbar != null && BookmarkStore.Data.Bar.Children!.Count == 0)
        {
            ParentRemove(root, toolbar);
            foreach (var c in toolbar.Children!) BookmarkStore.Insert(BookmarkData.BarId, c);
        }
        if (root.Children!.Count > 0) BookmarkStore.Insert(BookmarkData.OtherId, root);
        return count;
    }

    private static void ParentRemove(BookmarkNode root, BookmarkNode target)
    {
        foreach (var f in new[] { root }.Concat(root.Descendants()).Where(n => n.IsFolder))
            if (f.Children!.Remove(target)) return;
    }
}
