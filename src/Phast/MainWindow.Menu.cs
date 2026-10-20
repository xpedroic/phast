using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Phast.Input;
using Phast.Session;

namespace Phast;

/// <summary>The three-dot application menu (built in code so it always reflects current state and shortcuts).</summary>
public partial class MainWindow
{
    // Chrome's zoom steps.
    private static readonly double[] ZoomLevels = [0.25, 0.33, 0.5, 0.67, 0.75, 0.8, 0.9, 1, 1.1, 1.25, 1.5, 1.75, 2, 2.5, 3, 4, 5];

    private void Menu_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu
        {
            PlacementTarget = MenuButton,
            Placement = PlacementMode.Bottom,
            HorizontalOffset = -250,
        };

        menu.Items.Add(Item("Nueva pestaña", "", Commands.NewTab));
        menu.Items.Add(Item("Nueva ventana", "", Commands.NewWindow));
        var reopen = Item("Reabrir pestaña cerrada", "", Commands.ReopenTab);
        reopen.IsEnabled = Tabs.HasClosedTabs;
        menu.Items.Add(reopen);
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Historial", "", Commands.History));
        menu.Items.Add(Item("Descargas", "", Commands.Downloads));
        menu.Items.Add(BuildBookmarksItem());
        menu.Items.Add(new Separator());
        menu.Items.Add(BuildZoomItem());
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Imprimir...", "", Commands.Print));
        menu.Items.Add(Item("Pantalla completa", "", Commands.FullScreen));
        var dark = Item("Modo oscuro en esta web", "", Commands.DarkMode);
        dark.IsChecked = Tabs.Active?.IsDarkForced == true;
        dark.IsEnabled = DarkButton.IsEnabled;
        menu.Items.Add(dark);
        menu.Items.Add(Item("Herramientas para desarrolladores", "", Commands.DevTools));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Borrar datos de navegación...", "", Commands.ClearData));
        menu.Items.Add(Item("Configuración", "", Commands.Settings));
        var exit = new MenuItem { Header = "Salir", Icon = Glyph("") };
        exit.Click += (_, _) => SessionManager.ExitAll();
        menu.Items.Add(exit);

        menu.IsOpen = true;
    }

    private MenuItem Item(string header, string glyph, string command)
    {
        var item = new MenuItem
        {
            Header = header,
            Icon = Glyph(glyph),
            InputGestureText = App.Shortcuts.GestureText(command),
        };
        item.Click += (_, _) => Execute(command);
        return item;
    }

    private static TextBlock Glyph(string glyph) => new() { Text = glyph };

    /// <summary>"Marcadores" submenu, like Chrome's.</summary>
    private MenuItem BuildBookmarksItem()
    {
        var root = new MenuItem { Header = "Marcadores", Icon = Glyph("") };
        root.Items.Add(Item("Añadir esta pestaña a marcadores...", "", Commands.Bookmark));
        var toggle = Item("Mostrar barra de marcadores", "", Commands.ToggleBookmarksBar);
        toggle.IsChecked = Settings.SettingsService.Current.ShowBookmarksBar;
        root.Items.Add(toggle);
        root.Items.Add(Item("Gestor de marcadores", "", Commands.BookmarkManager));
        var import = new MenuItem { Header = "Importar marcadores y ajustes...", Icon = Glyph("") };
        import.Click += (_, _) => OpenInternalPage("phast://settings#bookmarks");
        root.Items.Add(import);
        return root;
    }

    /// <summary>"Zoom   −  100 %  +" row, which stays open while clicking.</summary>
    private MenuItem BuildZoomItem()
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Width = 48, TextAlignment = TextAlignment.Center };
        void Refresh() => label.Text = $"{Math.Round((Tabs.Active?.WebView?.ZoomFactor ?? 1) * 100)} %";
        Refresh();

        Button Btn(string text, int direction)
        {
            var b = new Button { Content = text, Style = (Style)FindResource("MenuInlineButton") };
            b.Click += (_, _) => { StepZoom(direction); Refresh(); };
            return b;
        }

        var row = new DockPanel { Width = 230, LastChildFill = false };
        row.Children.Add(new TextBlock { Text = "Zoom", VerticalAlignment = VerticalAlignment.Center });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Btn("", -1));
        buttons.Children.Add(label);
        buttons.Children.Add(Btn("", +1));
        DockPanel.SetDock(buttons, Dock.Right);
        row.Children.Add(buttons);

        return new MenuItem { Header = row, StaysOpenOnClick = true, Icon = Glyph("") };
    }

    private void StepZoom(int direction)
    {
        var wv = Tabs.Active?.WebView;
        if (wv == null) return;
        var current = wv.ZoomFactor;
        var next = direction > 0
            ? ZoomLevels.FirstOrDefault(z => z > current + 0.001, ZoomLevels[^1])
            : ZoomLevels.LastOrDefault(z => z < current - 0.001, ZoomLevels[0]);
        wv.ZoomFactor = next;
    }
}
