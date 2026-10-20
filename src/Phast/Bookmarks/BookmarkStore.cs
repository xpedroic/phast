using System.IO;
using System.Windows.Threading;
using Phast.Core;

namespace Phast.Bookmarks;

/// <summary>
/// Loads, edits and saves bookmarks.json. All edits go through this class, which keeps a URL index
/// (for the star button) and notifies listeners (bookmarks bar, bookmark manager page) on change.
/// Must be used from the UI thread.
/// </summary>
public static class BookmarkStore
{
    private static BookmarkData? _data;
    private static HashSet<string> _urls = new(StringComparer.Ordinal);
    private static DispatcherTimer? _saveTimer;

    private static string FilePath => Path.Combine(AppPaths.Root, "bookmarks.json");

    public static BookmarkData Data => _data ??= LoadData();

    /// <summary>Raised after any change.</summary>
    public static event Action? Changed;

    private static BookmarkData LoadData()
    {
        var data = JsonFile.LoadOrDefault<BookmarkData>(FilePath);
        data.Bar.Id = BookmarkData.BarId;
        data.Other.Id = BookmarkData.OtherId;
        data.Bar.Children ??= [];
        data.Other.Children ??= [];
        _data = data;
        RebuildIndex();
        return data;
    }

    private static void RebuildIndex() =>
        _urls = new HashSet<string>(Data.Roots.SelectMany(r => r.Descendants()).Where(n => n.Url != null).Select(n => n.Url!), StringComparer.Ordinal);

    public static bool IsBookmarked(string? url)
    {
        _ = Data; // make sure bookmarks are loaded (and the index built)
        return url != null && _urls.Contains(url);
    }

    public static BookmarkNode? FindByUrl(string url) =>
        Data.Roots.SelectMany(r => r.Descendants()).FirstOrDefault(n => n.Url == url);

    public static BookmarkNode? Find(string id) =>
        Data.Roots.FirstOrDefault(r => r.Id == id) ?? Data.Roots.SelectMany(r => r.Descendants()).FirstOrDefault(n => n.Id == id);

    public static BookmarkNode? ParentOf(string id) =>
        Data.Roots.Concat(Data.Roots.SelectMany(r => r.Descendants()))
            .FirstOrDefault(n => n.Children?.Any(c => c.Id == id) == true);

    /// <summary>Every folder with its depth, for folder pickers.</summary>
    public static IEnumerable<(BookmarkNode Folder, int Depth)> Folders()
    {
        IEnumerable<(BookmarkNode, int)> Walk(BookmarkNode n, int depth)
        {
            yield return (n, depth);
            foreach (var c in n.Children!.Where(c => c.IsFolder))
                foreach (var x in Walk(c, depth + 1)) yield return x;
        }
        return Data.Roots.SelectMany(r => Walk(r, 0));
    }

    // ---------------------------------------------------------------- edits

    public static BookmarkNode Add(string parentId, string title, string url, int? index = null)
    {
        var node = BookmarkNode.Link(title, url);
        Insert(parentId, node, index);
        return node;
    }

    public static BookmarkNode AddFolder(string parentId, string title, int? index = null)
    {
        var node = BookmarkNode.Folder(title);
        Insert(parentId, node, index);
        return node;
    }

    /// <summary>Inserts an already built node (or subtree, e.g. from an import).</summary>
    public static void Insert(string parentId, BookmarkNode node, int? index = null)
    {
        var parent = Find(parentId) is { IsFolder: true } p ? p : Data.Other;
        var at = index is int i ? Math.Clamp(i, 0, parent.Children!.Count) : parent.Children!.Count;
        parent.Children.Insert(at, node);
        OnChanged();
    }

    public static void Update(string id, string? title, string? url)
    {
        var node = Find(id);
        if (node == null || id is BookmarkData.BarId or BookmarkData.OtherId) return;
        if (title != null) node.Title = title;
        if (url != null && !node.IsFolder) node.Url = url;
        OnChanged();
    }

    public static void Remove(string id)
    {
        var parent = ParentOf(id);
        if (parent == null) return;
        parent.Children!.RemoveAll(c => c.Id == id);
        OnChanged();
    }

    public static void RemoveUrl(string url)
    {
        foreach (var folder in Data.Roots.Concat(Data.Roots.SelectMany(r => r.Descendants())).Where(n => n.IsFolder).ToList())
            folder.Children!.RemoveAll(c => c.Url == url);
        OnChanged();
    }

    /// <summary>Moves a node into another folder at a position (folders can't be moved into themselves).</summary>
    public static void Move(string id, string newParentId, int index)
    {
        var node = Find(id);
        var oldParent = ParentOf(id);
        var newParent = Find(newParentId);
        if (node == null || oldParent == null || newParent?.IsFolder != true) return;
        if (node.IsFolder && (node == newParent || node.Descendants().Contains(newParent))) return;

        var oldIndex = oldParent.Children!.IndexOf(node);
        oldParent.Children.RemoveAt(oldIndex);
        if (oldParent == newParent && oldIndex < index) index--;
        newParent.Children!.Insert(Math.Clamp(index, 0, newParent.Children.Count), node);
        OnChanged();
    }

    /// <summary>Simple search over titles and URLs (every word must match).</summary>
    public static IEnumerable<BookmarkNode> Search(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return [];
        return Data.Roots.SelectMany(r => r.Descendants())
            .Where(n => n.Url != null && words.All(w =>
                n.Title.Contains(w, StringComparison.OrdinalIgnoreCase) || n.Url.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Replaces all bookmarks (configuration import).</summary>
    public static void ReplaceAll(BookmarkData data)
    {
        data.Bar.Id = BookmarkData.BarId;
        data.Other.Id = BookmarkData.OtherId;
        data.Bar.Children ??= [];
        data.Other.Children ??= [];
        _data = data;
        OnChanged();
    }

    // ---------------------------------------------------------------- persistence

    private static void OnChanged()
    {
        RebuildIndex();
        ScheduleSave();
        Changed?.Invoke();
    }

    private static void ScheduleSave()
    {
        if (_saveTimer == null)
        {
            _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
        }
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Writes pending changes immediately (also called on exit).</summary>
    public static void SaveNow()
    {
        _saveTimer?.Stop();
        if (_data == null) return;
        try { JsonFile.Save(FilePath, _data); }
        catch (Exception ex) { Log.Write($"Bookmarks save failed: {ex.Message}"); }
    }
}
