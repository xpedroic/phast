using System.Text.Json.Serialization;

namespace Phast.Bookmarks;

/// <summary>A bookmark (has a <see cref="Url"/>) or a folder (has <see cref="Children"/>).</summary>
public sealed class BookmarkNode
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public List<BookmarkNode>? Children { get; set; }
    /// <summary>Creation time, Unix milliseconds.</summary>
    public long Added { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    [JsonIgnore] public bool IsFolder => Children != null;

    public static BookmarkNode Folder(string title, string? id = null) =>
        new() { Id = id ?? Guid.NewGuid().ToString("N")[..12], Title = title, Children = [] };

    public static BookmarkNode Link(string title, string url) => new() { Title = title, Url = url };

    /// <summary>All nodes below this one (depth first).</summary>
    public IEnumerable<BookmarkNode> Descendants()
    {
        if (Children == null) yield break;
        foreach (var c in Children)
        {
            yield return c;
            foreach (var d in c.Descendants()) yield return d;
        }
    }
}

/// <summary>Root of bookmarks.json: the bookmarks bar and "Other bookmarks", like Chrome.</summary>
public sealed class BookmarkData
{
    public const string BarId = "bar";
    public const string OtherId = "other";

    public BookmarkNode Bar { get; set; } = BookmarkNode.Folder("Barra de marcadores", BarId);
    public BookmarkNode Other { get; set; } = BookmarkNode.Folder("Otros marcadores", OtherId);

    public IEnumerable<BookmarkNode> Roots => [Bar, Other];
}
