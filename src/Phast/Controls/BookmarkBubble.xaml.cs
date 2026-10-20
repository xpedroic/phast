using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Phast.Bookmarks;

namespace Phast.Controls;

/// <summary>What the bubble should show and do.</summary>
public sealed class BubbleRequest
{
    public required string Heading { get; init; }
    public string Name { get; init; } = "";
    /// <summary>Null hides the URL field.</summary>
    public string? Url { get; init; }
    /// <summary>Null hides the folder picker.</summary>
    public string? FolderId { get; init; }
    public string PrimaryText { get; init; } = "Hecho";
    public string SecondaryText { get; init; } = "Cancelar";
    /// <summary>Called with (name, url, folderId) to apply the changes.</summary>
    public required Action<string, string?, string?> Save { get; init; }
    /// <summary>Secondary button action (e.g. remove). Null = just close.</summary>
    public Action? Secondary { get; init; }
    /// <summary>Apply the changes also when the bubble is dismissed by clicking elsewhere (Chrome's star bubble).</summary>
    public bool SaveOnDismiss { get; init; }
}

/// <summary>
/// Small Chrome-like bubble used for "Bookmark added", editing bookmarks and creating pages/folders.
/// Lives inside a Popup owned by the window.
/// </summary>
public partial class BookmarkBubble : UserControl
{
    private Popup? _popup;
    private BubbleRequest? _request;
    private bool _handled;

    public BookmarkBubble() => InitializeComponent();

    public void Show(Popup popup, UIElement anchor, BubbleRequest request)
    {
        _popup = popup;
        _request = request;
        _handled = false;

        Heading.Text = request.Heading;
        NameBox.Text = request.Name;
        UrlPanel.Visibility = request.Url != null ? Visibility.Visible : Visibility.Collapsed;
        UrlBox.Text = request.Url ?? "";
        FolderPanel.Visibility = request.FolderId != null ? Visibility.Visible : Visibility.Collapsed;
        PrimaryAction.Content = request.PrimaryText;
        SecondaryAction.Content = request.SecondaryText;

        FolderBox.Items.Clear();
        if (request.FolderId != null)
        {
            foreach (var (folder, depth) in BookmarkStore.Folders())
            {
                var item = new ComboBoxItem { Content = new string(' ', depth * 4) + folder.Title, Tag = folder.Id };
                FolderBox.Items.Add(item);
                if (folder.Id == request.FolderId) FolderBox.SelectedItem = item;
            }
        }

        popup.Closed -= OnPopupClosed;
        popup.Closed += OnPopupClosed;
        popup.PlacementTarget = anchor;
        popup.Placement = PlacementMode.Bottom;
        // Right-align the bubble with its anchor when the anchor is on the right half of the window (the star).
        var window = Window.GetWindow(anchor);
        var x = anchor.TranslatePoint(new Point(0, 0), window).X;
        popup.HorizontalOffset = x > window.ActualWidth / 2 ? -(Width - ((FrameworkElement)anchor).ActualWidth) + 8 : -8;
        popup.IsOpen = true;

        Dispatcher.BeginInvoke(() => { NameBox.Focus(); NameBox.SelectAll(); }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Apply()
    {
        if (_request == null || _handled) return;
        _handled = true;
        var folder = (FolderBox.SelectedItem as ComboBoxItem)?.Tag as string;
        _request.Save(NameBox.Text.Trim(), _request.Url != null ? UrlBox.Text.Trim() : null, folder);
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        Apply();
        Close();
    }

    private void Secondary_Click(object sender, RoutedEventArgs e)
    {
        _handled = true;
        _request?.Secondary?.Invoke();
        Close();
    }

    private void Box_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Apply(); Close(); e.Handled = true; }
        else if (e.Key == Key.Escape) { _handled = !(_request?.SaveOnDismiss ?? false); if (!_handled) Apply(); Close(); e.Handled = true; }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (_request?.SaveOnDismiss == true) Apply();
        _handled = true;
    }

    private void Close()
    {
        if (_popup != null) _popup.IsOpen = false;
    }
}
