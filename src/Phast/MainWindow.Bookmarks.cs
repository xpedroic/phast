using System.Windows;
using Phast.Bookmarks;
using Phast.Controls;
using Phast.Session;
using Phast.Settings;

namespace Phast;

/// <summary>Bookmarks in the window: star button / Ctrl+D bubble, bookmarks bar and its actions.</summary>
public partial class MainWindow
{
    private void InitBookmarks()
    {
        BookmarksBar.OpenRequested += OpenUrl;
        BookmarksBar.OpenManyRequested += OpenMany;
        BookmarksBar.EditRequested += EditBookmark;
        BookmarksBar.AddRequested += AddFromBar;
        BookmarksBar.ManagerRequested += () => Execute(Input.Commands.BookmarkManager);
        BookmarksBar.HideRequested += () => SettingsService.Update(s => s.ShowBookmarksBar = false);

        BookmarkStore.Changed += UpdateStar;
        SettingsService.Changed += UpdateBookmarksBarVisibility;
        Closed += (_, _) =>
        {
            BookmarkStore.Changed -= UpdateStar;
            SettingsService.Changed -= UpdateBookmarksBarVisibility;
        };
        UpdateBookmarksBarVisibility();
    }

    private void UpdateBookmarksBarVisibility() =>
        BookmarksBar.Visibility = SettingsService.Current.ShowBookmarksBar && !_isFullScreen ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Filled blue star when the current page is bookmarked.</summary>
    private void UpdateStar()
    {
        var bookmarked = BookmarkStore.IsBookmarked(Tabs.Active?.Url);
        StarButton.Content = bookmarked ? "" : "";
        StarButton.ToolTip = bookmarked ? "Editar marcador de esta pestaña (Ctrl+D)" : "Añadir a marcadores (Ctrl+D)";
        if (bookmarked) StarButton.SetResourceReference(ForegroundProperty, "AccentBrush");
        else StarButton.ClearValue(ForegroundProperty);
    }

    private void Star_Click(object sender, RoutedEventArgs e) => BookmarkCurrentPage();

    /// <summary>Ctrl+D: bookmark the page (in the bar) and show the bubble to rename / move it.</summary>
    public void BookmarkCurrentPage()
    {
        var tab = Tabs.Active;
        if (tab == null || string.IsNullOrEmpty(tab.Url)) return;

        var existing = BookmarkStore.FindByUrl(tab.Url);
        var isNew = existing == null;
        var node = existing ?? BookmarkStore.Add(BookmarkData.BarId, tab.Title.Length > 0 ? tab.Title : tab.DisplayTitle, tab.Url);
        if (isNew && tab.FaviconPng != null) Core.FaviconCache.Save(tab.Url, tab.FaviconPng);

        Bubble.Show(BubblePopup, StarButton, new BubbleRequest
        {
            Heading = isNew ? "Marcador añadido" : "Editar marcador",
            Name = node.Title,
            FolderId = BookmarkStore.ParentOf(node.Id)?.Id ?? BookmarkData.BarId,
            PrimaryText = "Hecho",
            SecondaryText = "Quitar",
            SaveOnDismiss = true,
            Save = (name, _, folder) => SaveNode(node, name, null, folder),
            Secondary = () => BookmarkStore.Remove(node.Id),
        });
    }

    private void EditBookmark(BookmarkNode node, UIElement anchor)
    {
        Bubble.Show(BubblePopup, anchor, new BubbleRequest
        {
            Heading = node.IsFolder ? "Cambiar nombre de la carpeta" : "Editar marcador",
            Name = node.Title,
            Url = node.IsFolder ? null : node.Url,
            FolderId = BookmarkStore.ParentOf(node.Id)?.Id,
            PrimaryText = "Guardar",
            Save = (name, url, folder) => SaveNode(node, name, url, folder),
        });
    }

    private void AddFromBar(string parentId, bool folder)
    {
        var tab = Tabs.Active;
        Bubble.Show(BubblePopup, BookmarksBar, new BubbleRequest
        {
            Heading = folder ? "Nueva carpeta" : "Añadir página",
            Name = folder ? "Nueva carpeta" : tab?.Title ?? "",
            Url = folder ? null : tab?.Url ?? "",
            FolderId = parentId,
            PrimaryText = "Guardar",
            Save = (name, url, parent) =>
            {
                if (folder) BookmarkStore.AddFolder(parent ?? parentId, name.Length > 0 ? name : "Nueva carpeta");
                else if (Core.UrlResolver.Resolve(url ?? "") is { } resolved)
                    BookmarkStore.Add(parent ?? parentId, name.Length > 0 ? name : resolved, resolved);
            },
        });
    }

    private static void SaveNode(BookmarkNode node, string name, string? url, string? folderId)
    {
        if (BookmarkStore.Find(node.Id) == null) return; // removed meanwhile
        var resolvedUrl = url != null ? Core.UrlResolver.Resolve(url) : null;
        BookmarkStore.Update(node.Id, name.Length > 0 ? name : node.Title, resolvedUrl);
        var parent = BookmarkStore.ParentOf(node.Id);
        if (folderId != null && parent != null && parent.Id != folderId)
            BookmarkStore.Move(node.Id, folderId, int.MaxValue);
    }

    /// <summary>Opens a URL as requested by a bookmark click.</summary>
    public void OpenUrl(string url, OpenMode mode)
    {
        switch (mode)
        {
            case OpenMode.CurrentTab:
                if (Tabs.Active != null) Tabs.Active.Navigate(url);
                else Tabs.Open(url);
                FocusPage();
                break;
            case OpenMode.BackgroundTab: Tabs.OpenNextToActive(url, activate: false); break;
            case OpenMode.ForegroundTab: Tabs.OpenNextToActive(url, activate: true); break;
            case OpenMode.NewWindow: SessionManager.OpenWindow(null, [url]); break;
        }
    }

    private void OpenMany(List<string> urls, bool newWindow)
    {
        if (urls.Count == 0) return;
        if (newWindow)
        {
            var window = SessionManager.OpenWindow(null, urls.Take(1));
            foreach (var u in urls.Skip(1)) window.Tabs.Open(u, activate: false);
        }
        else
        {
            foreach (var u in urls) Tabs.Open(u, activate: false);
        }
    }
}
