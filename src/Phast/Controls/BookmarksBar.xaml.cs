using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Phast.Bookmarks;
using Phast.Core;

namespace Phast.Controls;

/// <summary>How a bookmark should be opened.</summary>
public enum OpenMode { CurrentTab, BackgroundTab, ForegroundTab, NewWindow }

/// <summary>
/// The bookmarks bar below the toolbar: bookmarks and folders (as dropdown menus), an overflow "»"
/// menu for items that don't fit, and "Otros marcadores" on the right. Right click offers edit/delete.
/// </summary>
public partial class BookmarksBar : UserControl
{
    private const double ChevronWidth = 30;

    public BookmarksBar()
    {
        InitializeComponent();
        SizeChanged += (_, _) => LayoutOverflow();
        Loaded += (_, _) => { BookmarkStore.Changed += Rebuild; FaviconCache.Saved += OnFaviconSaved; Rebuild(); };
        Unloaded += (_, _) => { BookmarkStore.Changed -= Rebuild; FaviconCache.Saved -= OnFaviconSaved; };
    }

    /// <summary>A site's icon became known: refresh the bar if one of its bookmarks belongs to that site.</summary>
    private void OnFaviconSaved(string host)
    {
        if (!Dispatcher.CheckAccess()) return; // imports refresh the bar through BookmarkStore.Changed
        if (BookmarkStore.Data.Bar.Children!.Any(n => FaviconCache.HostOf(n.Url) == host)) Rebuild();
    }

    /// <summary>The user picked a bookmark.</summary>
    public event Action<string, OpenMode>? OpenRequested;
    /// <summary>"Open all" on a folder (urls, inNewWindow).</summary>
    public event Action<List<string>, bool>? OpenManyRequested;
    /// <summary>The user wants to edit a bookmark/folder (anchor = clicked element).</summary>
    public event Action<BookmarkNode, UIElement>? EditRequested;
    /// <summary>Open the bookmark manager.</summary>
    public event Action? ManagerRequested;
    /// <summary>"Add page" / "Add folder" from the bar's context menu (parent folder id, isFolder).</summary>
    public event Action<string, bool>? AddRequested;
    /// <summary>"Import bookmarks now" link shown on an empty bar.</summary>
    public event Action? ImportRequested;
    /// <summary>Toggle the bar's visibility from its own context menu.</summary>
    public event Action? HideRequested;

    // ---------------------------------------------------------------- building

    private void Rebuild()
    {
        ItemsHost.Children.Clear();
        foreach (var node in BookmarkStore.Data.Bar.Children!)
            ItemsHost.Children.Add(CreateButton(node));

        EmptyHint.Visibility = ItemsHost.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var other = BookmarkStore.Data.Other;
        OtherArea.Visibility = other.Children!.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        OtherButton.Content = ButtonContent(other);
        OtherButton.Tag = other;
        Dispatcher.BeginInvoke(LayoutOverflow, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private Button CreateButton(BookmarkNode node)
    {
        var button = new Button
        {
            Style = (Style)FindResource("BookmarkButton"),
            Content = ButtonContent(node),
            Tag = node,
            ToolTip = node.IsFolder ? node.Title : $"{node.Title}\n{node.Url}",
        };
        button.Click += (_, _) =>
        {
            if (node.IsFolder) ShowFolderMenu(node, button);
            else OpenRequested?.Invoke(node.Url!, ModeFromModifiers());
        };
        button.MouseUp += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle && !node.IsFolder)
            {
                OpenRequested?.Invoke(node.Url!, OpenMode.BackgroundTab);
                e.Handled = true;
            }
        };
        button.MouseRightButtonUp += (_, e) => { ShowItemContextMenu(node, button); e.Handled = true; };
        return button;
    }

    private static object ButtonContent(BookmarkNode node)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(Icon(node));
        if (node.Title.Length > 0)
            panel.Children.Add(new TextBlock
            {
                Text = node.Title,
                Margin = new Thickness(6, 0, 0, 1),
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
        return panel;
    }

    /// <summary>Favicon, folder glyph or a generic globe.</summary>
    public static FrameworkElement Icon(BookmarkNode node)
    {
        if (!node.IsFolder && FaviconCache.Get(node.Url) is { } img)
            return new Image { Source = img, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        var glyph = new TextBlock
        {
            Text = node.IsFolder ? "" : "",
            FontFamily = (FontFamily)Application.Current.FindResource("IconFont"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, "IconBrush");
        return glyph;
    }

    /// <summary>Hides items that don't fit and shows the "»" chevron for them.</summary>
    private void LayoutOverflow()
    {
        var available = Dock.ActualWidth - (OtherArea.Visibility == Visibility.Visible ? OtherArea.ActualWidth : 0);
        var children = ItemsHost.Children.OfType<FrameworkElement>().ToList();
        var widths = children.Select(c =>
        {
            c.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return c.DesiredSize.Width;
        }).ToList();

        // Everything fits: no chevron. Otherwise keep room for the chevron and hide the rest.
        var limit = widths.Sum() <= available ? available : available - ChevronWidth;
        double used = 0;
        var anyHidden = false;
        for (int i = 0; i < children.Count; i++)
        {
            used += widths[i];
            var visible = !anyHidden && used <= limit;
            children[i].Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            anyHidden |= !visible;
        }
        ChevronButton.Visibility = anyHidden ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------------------------------------------------------- menus

    private static OpenMode ModeFromModifiers()
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return OpenMode.NewWindow;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return OpenMode.BackgroundTab;
        return OpenMode.CurrentTab;
    }

    private void ShowFolderMenu(BookmarkNode folder, UIElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom, MinWidth = 220 };
        FillFolderItems(menu.Items, folder.Children!);
        if (folder.Children!.Count == 0) menu.Items.Add(new MenuItem { Header = "(vacía)", IsEnabled = false });
        menu.IsOpen = true;
    }

    private void FillFolderItems(ItemCollection items, IEnumerable<BookmarkNode> nodes)
    {
        foreach (var node in nodes)
        {
            var item = new MenuItem { Header = node.Title.Length > 0 ? node.Title : node.Url, Icon = Icon(node), Tag = node };
            if (node.IsFolder)
            {
                FillFolderItems(item.Items, node.Children!);
                if (node.Children!.Count == 0) item.Items.Add(new MenuItem { Header = "(vacía)", IsEnabled = false });
            }
            else
            {
                item.ToolTip = node.Url;
                item.Click += (_, _) => OpenRequested?.Invoke(node.Url!, ModeFromModifiers());
                item.MouseUp += (_, e) =>
                {
                    if (e.ChangedButton != MouseButton.Middle) return;
                    OpenRequested?.Invoke(node.Url!, OpenMode.BackgroundTab);
                    e.Handled = true;
                };
            }
            items.Add(item);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e) => ImportRequested?.Invoke();

    private void Other_Click(object sender, RoutedEventArgs e) => ShowFolderMenu(BookmarkStore.Data.Other, OtherButton);

    private void Chevron_Click(object sender, RoutedEventArgs e)
    {
        var hidden = ItemsHost.Children.OfType<FrameworkElement>()
            .Where(c => c.Visibility == Visibility.Collapsed)
            .Select(c => (BookmarkNode)c.Tag);
        var menu = new ContextMenu { PlacementTarget = ChevronButton, Placement = PlacementMode.Bottom, MinWidth = 220 };
        FillFolderItems(menu.Items, hidden);
        menu.IsOpen = true;
    }

    private void ShowItemContextMenu(BookmarkNode node, UIElement anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.MousePoint };
        if (node.IsFolder)
        {
            var urls = node.Descendants().Where(n => n.Url != null).Select(n => n.Url!).ToList();
            menu.Items.Add(Item($"Abrir todo ({urls.Count})", "", () => OpenManyRequested?.Invoke(urls, false), urls.Count > 0));
            menu.Items.Add(Item("Abrir todo en una ventana nueva", "", () => OpenManyRequested?.Invoke(urls, true), urls.Count > 0));
        }
        else
        {
            menu.Items.Add(Item("Abrir en una pestaña nueva", "", () => OpenRequested?.Invoke(node.Url!, OpenMode.ForegroundTab)));
            menu.Items.Add(Item("Abrir en una ventana nueva", "", () => OpenRequested?.Invoke(node.Url!, OpenMode.NewWindow)));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(node.IsFolder ? "Cambiar nombre..." : "Editar...", "", () => EditRequested?.Invoke(node, anchor)));
        menu.Items.Add(Item("Eliminar", "", () => BookmarkStore.Remove(node.Id)));
        menu.Items.Add(new Separator());
        AddCommonItems(menu, BookmarkStore.ParentOf(node.Id)?.Id ?? BookmarkData.BarId);
        menu.IsOpen = true;
    }

    private void Bar_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        AddCommonItems(menu, BookmarkData.BarId);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void AddCommonItems(ContextMenu menu, string parentId)
    {
        menu.Items.Add(Item("Añadir página...", "", () => AddRequested?.Invoke(parentId, false)));
        menu.Items.Add(Item("Añadir carpeta...", "", () => AddRequested?.Invoke(parentId, true)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Gestor de marcadores", "", () => ManagerRequested?.Invoke()));
        menu.Items.Add(Item("Ocultar la barra de marcadores", "", () => HideRequested?.Invoke()));
    }

    private static MenuItem Item(string header, string glyph, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, Icon = new TextBlock { Text = glyph }, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }
}
