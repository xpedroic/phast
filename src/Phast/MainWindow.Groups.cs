using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Phast.Core;
using Phast.Session;
using Phast.Settings;
using Phast.Tabs;

namespace Phast;

/// <summary>Tab groups in the window: group labels, tab context menu, and the page groups button.</summary>
public partial class MainWindow
{
    // ---------------------------------------------------------------- page groups button (toolbar)

    private void PageGroups_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = PageGroupsButton, Placement = PlacementMode.Bottom, MinWidth = 260 };
        var groups = SettingsService.Current.PageGroups;
        if (groups.Count == 0)
            menu.Items.Add(new MenuItem { Header = "Todavía no tienes grupos de páginas", IsEnabled = false });

        foreach (var pg in groups)
        {
            var item = new MenuItem
            {
                Header = pg.Name.Length > 0 ? pg.Name : "(sin nombre)",
                Icon = ColorDot(pg.Color),
                InputGestureText = pg.Urls.Count == 1 ? "1 página" : $"{pg.Urls.Count} páginas",
                IsEnabled = pg.Urls.Count > 0,
            };
            item.Click += (_, _) => Tabs.OpenPageGroup(pg);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Crear grupo con las pestañas abiertas", "", SaveOpenTabsAsPageGroup));
        menu.Items.Add(MenuAction("Gestionar grupos de páginas...", "", () => OpenInternalPage("phast://settings#groups")));
        menu.IsOpen = true;
    }

    private void SaveOpenTabsAsPageGroup()
    {
        var urls = Tabs.Tabs.Select(t => t.Url).Where(u => u.Length > 0 && !InternalPages.IsInternal(u)).Distinct().ToList();
        var name = $"Grupo {SettingsService.Current.PageGroups.Count + 1}";
        SettingsService.Update(s => s.PageGroups.Add(new PageGroup
        {
            Name = name,
            Color = GroupColors.Next(s.PageGroups.Select(g => g.Color)),
            Urls = urls,
        }));
        OpenInternalPage("phast://settings#groups");
    }

    private static Ellipse ColorDot(string color) => new()
    {
        Width = 12,
        Height = 12,
        Fill = new SolidColorBrush(GroupColors.ColorOf(color)),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static MenuItem MenuAction(string header, string glyph, Action action, bool enabled = true)
    {
        var item = new MenuItem { Header = header, Icon = new TextBlock { Text = glyph }, IsEnabled = enabled };
        item.Click += (_, _) => action();
        return item;
    }

    // ---------------------------------------------------------------- group label ("chip")

    private void GroupChip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TabGroup g) Tabs.SetCollapsed(g, !g.IsCollapsed);
    }

    private void GroupChip_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TabGroup g) return;
        e.Handled = true;
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.Bottom, MinWidth = 260 };

        // Name and color editor at the top, like Chrome's group bubble.
        var name = new TextBox { Text = g.Name, Style = (Style)FindResource("FormTextBox"), Width = 220 };
        name.TextChanged += (_, _) => { g.Name = name.Text; SessionManager.MarkDirty(); };
        name.KeyDown += (_, k) => { if (k.Key is Key.Enter or Key.Escape) menu.IsOpen = false; };
        var colors = new WrapPanel { Margin = new Thickness(0, 10, 0, 4), Width = 230 };
        foreach (var c in GroupColors.All)
        {
            var dot = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(GroupColors.ColorOf(c.Id)), Cursor = Cursors.Hand, ToolTip = c.Name,
                BorderThickness = new Thickness(c.Id == g.Color ? 2 : 0),
            };
            dot.SetResourceReference(Border.BorderBrushProperty, "TextBrush");
            dot.MouseLeftButtonUp += (_, _) => { g.Color = c.Id; menu.IsOpen = false; SessionManager.MarkDirty(); };
            colors.Children.Add(dot);
        }
        var editor = new StackPanel { Margin = new Thickness(16, 8, 16, 8) };
        editor.Children.Add(name);
        editor.Children.Add(colors);
        menu.Items.Add(new MenuItem { Header = editor, StaysOpenOnClick = true, Focusable = false,
            Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse(
                "<ControlTemplate TargetType=\"MenuItem\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><ContentPresenter ContentSource=\"Header\"/></ControlTemplate>") });
        menu.Items.Add(new Separator());

        menu.Items.Add(MenuAction("Nueva pestaña en el grupo", "", () =>
        {
            var last = Tabs.Tabs.Last(t => t.Group == g);
            var tab = Tabs.Open(null, activate: true, index: Tabs.Tabs.IndexOf(last) + 1);
            Tabs.SetGroup(tab, g);
        }));
        menu.Items.Add(MenuAction(g.IsCollapsed ? "Expandir grupo" : "Contraer grupo", g.IsCollapsed ? "" : "",
            () => Tabs.SetCollapsed(g, !g.IsCollapsed)));
        menu.Items.Add(MenuAction("Desagrupar", "", () => Tabs.Ungroup(g)));
        var saved = SettingsService.Current.PageGroups.FirstOrDefault(p => p.Id == g.PageGroupId);
        menu.Items.Add(MenuAction(saved == null ? "Guardar como grupo de páginas" : "Actualizar el grupo de páginas guardado", "",
            () => SaveTabGroup(g)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Cerrar grupo", "", () => Tabs.CloseGroup(g)));
        menu.IsOpen = true;
        Dispatcher.BeginInvoke(() => { name.Focus(); name.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>Stores (or updates) a tab group as a page group in Settings, so it can be reopened later.</summary>
    private void SaveTabGroup(TabGroup g)
    {
        var urls = Tabs.Tabs.Where(t => t.Group == g).Select(t => t.Url).Where(u => u.Length > 0 && !InternalPages.IsInternal(u)).ToList();
        SettingsService.Update(s =>
        {
            var pg = s.PageGroups.FirstOrDefault(p => p.Id == g.PageGroupId);
            if (pg == null)
            {
                pg = new PageGroup();
                s.PageGroups.Add(pg);
                g.PageGroupId = pg.Id;
            }
            pg.Name = g.Name.Length > 0 ? g.Name : $"Grupo {s.PageGroups.Count}";
            pg.Color = g.Color;
            pg.Urls = urls;
        });
    }

    // ---------------------------------------------------------------- tab context menu

    private void Tab_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (TabOf(sender) is not { } tab) return;
        e.Handled = true;
        var index = Tabs.Tabs.IndexOf(tab);
        var menu = new ContextMenu { PlacementTarget = (UIElement)sender, Placement = PlacementMode.MousePoint };

        menu.Items.Add(MenuAction("Nueva pestaña a la derecha", "", () => Tabs.Open(null, activate: true, index: Tabs.Tabs.IndexOf(tab) + 1)));
        if (tab.Group == null)
        {
            menu.Items.Add(MenuAction("Añadir pestaña a un grupo nuevo", "", () => Tabs.GroupTab(tab)));
            var others = Tabs.Groups.ToList();
            if (others.Count > 0)
            {
                var add = new MenuItem { Header = "Añadir pestaña a un grupo", Icon = new TextBlock { Text = "" } };
                foreach (var g in others)
                {
                    var item = new MenuItem { Header = g.Name.Length > 0 ? g.Name : "(sin nombre)", Icon = ColorDot(g.Color) };
                    item.Click += (_, _) => Tabs.SetGroup(tab, g);
                    add.Items.Add(item);
                }
                menu.Items.Add(add);
            }
        }
        else menu.Items.Add(MenuAction("Quitar del grupo", "", () => Tabs.SetGroup(tab, null)));

        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Recargar", "", () => { if (tab.Core != null) tab.Core.Reload(); else Tabs.Activate(tab); }));
        menu.Items.Add(MenuAction("Duplicar", "", () =>
        {
            var copy = Tabs.Open(tab.Url, activate: true, index: Tabs.Tabs.IndexOf(tab) + 1);
            if (tab.Group != null) Tabs.SetGroup(copy, tab.Group);
        }, tab.Url.Length > 0));
        menu.Items.Add(MenuAction(tab.IsMuted ? "Activar sonido de la pestaña" : "Silenciar pestaña", tab.IsMuted ? "" : "",
            tab.ToggleMute, tab.HasWebView));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuAction("Cerrar", "", () => Tabs.Close(tab)));
        menu.Items.Add(MenuAction("Cerrar otras pestañas", "", () =>
        {
            foreach (var t in Tabs.Tabs.Where(t => t != tab).ToList()) Tabs.Close(t);
        }, Tabs.Tabs.Count > 1));
        menu.Items.Add(MenuAction("Cerrar pestañas a la derecha", "", () =>
        {
            foreach (var t in Tabs.Tabs.Skip(Tabs.Tabs.IndexOf(tab) + 1).ToList()) Tabs.Close(t);
        }, index < Tabs.Tabs.Count - 1));
        menu.IsOpen = true;
    }
}
