using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Phast.Core;
using Phast.History;

namespace Phast.Omnibox;

/// <summary>
/// Drives the address bar dropdown: queries suggestions while typing, inline autocompletion,
/// keyboard navigation (↑ ↓ Enter Esc, Shift+Del to forget a history entry) and mouse selection.
/// </summary>
public sealed class OmniboxController
{
    private readonly TextBox _box;
    private readonly Popup _popup;
    private readonly Action<string, bool> _navigate;
    private readonly DispatcherTimer _searchDebounce;

    private string _userText = "";       // exactly what the user typed (without inline completion)
    private string _rowsFor = "";        // text the current rows were computed for
    private string _completion = "";     // address bar text including inline completion
    private int _selected = -1;
    private bool _suppress;              // ignore TextChanged caused by our own edits
    private bool _lastWasDeletion;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<Suggestion> Items { get; } = new();

    /// <param name="navigate">Called with (url, openInNewTab) when the user picks something.</param>
    public OmniboxController(TextBox box, Popup popup, ItemsControl list, Action<string, bool> navigate)
    {
        _box = box;
        _popup = popup;
        _navigate = navigate;
        list.ItemsSource = Items;

        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _searchDebounce.Tick += async (_, _) => { _searchDebounce.Stop(); await FetchSearchSuggestionsAsync(); };

        box.TextChanged += OnTextChanged;
        box.PreviewKeyDown += OnPreviewKeyDown;
        box.PreviewTextInput += (_, _) => _lastWasDeletion = false;
        box.LostKeyboardFocus += (_, _) => Close();
    }

    public bool IsOpen => _popup.IsOpen;

    /// <summary>The user changed the text since it was last set by the browser.</summary>
    public bool UserEdited { get; private set; }

    /// <summary>Changes the address bar text from code without triggering suggestions.</summary>
    public void SetText(string text)
    {
        Close();
        _suppress = true;
        _box.Text = text;
        _suppress = false;
        _userText = _completion = text;
        UserEdited = false;
    }

    // ------------------------------------------------------------ typing

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress || !_box.IsKeyboardFocusWithin) return;
        UserEdited = true;
        _userText = _box.Text;
        _completion = _userText;
        _ = UpdateAsync();
    }

    private async Task UpdateAsync()
    {
        var typed = _userText;
        if (typed.Trim().Length == 0) { Close(); return; }

        LocalSuggestions local;
        try { local = await SuggestionProvider.GetLocalAsync(typed); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Suggestions failed: {ex.Message}"); return; }
        if (typed != _userText || !_box.IsKeyboardFocusWithin) return; // user kept typing; a newer update will follow

        ApplyInlineCompletion(typed, local.InlineCompletion);

        Items.Clear();
        foreach (var row in local.Rows) Items.Add(row);
        _rowsFor = typed;
        Select(0, updateText: false);
        _popup.IsOpen = Items.Count > 0;

        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    /// <summary>"gi|" becomes "gi[thub.com]" with the completed part selected, so typing on simply replaces it.</summary>
    private void ApplyInlineCompletion(string typed, string? completion)
    {
        var caretAtEnd = _box.CaretIndex == _box.Text.Length && _box.SelectionLength == 0;
        if (_lastWasDeletion || !caretAtEnd || completion == null || completion.Length <= typed.Length) return;

        _suppress = true;
        _box.Text = completion;
        _box.Select(typed.Length, completion.Length - typed.Length);
        _suppress = false;
        _completion = completion;
    }

    private async Task FetchSearchSuggestionsAsync()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        var typed = _userText;

        var searches = await SuggestionProvider.GetSearchAsync(typed, cts.Token);
        if (cts.IsCancellationRequested || typed != _userText || typed != _rowsFor || !_popup.IsOpen) return;

        // Replace previous search rows (keep the first "default" row), avoid duplicates.
        for (int i = Items.Count - 1; i >= 1; i--)
            if (Items[i].Kind == SuggestionKind.Search) Items.RemoveAt(i);
        foreach (var s in searches)
        {
            if (Items.Count >= 9) break;
            if (Items.Any(r => r.Target == s.Target)) continue;
            Items.Add(s);
        }
        if (_selected >= Items.Count) Select(0, updateText: false);
    }

    // ------------------------------------------------------------ keys

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Back:
            case Key.Delete when !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift):
                _lastWasDeletion = true;
                break;

            case Key.Delete when Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && IsOpen:
                ForgetSelected();
                e.Handled = true;
                break;

            case Key.Down when IsOpen:
                Select(Math.Min(_selected + 1, Items.Count - 1), updateText: true);
                e.Handled = true;
                break;

            case Key.Up when IsOpen:
                Select(Math.Max(_selected - 1, 0), updateText: true);
                e.Handled = true;
                break;

            case Key.Escape when IsOpen:
                // First Esc removes the dropdown and the inline completion; the next one reverts the address (MainWindow).
                Close();
                _suppress = true;
                _box.Text = _userText;
                _box.CaretIndex = _box.Text.Length;
                _suppress = false;
                e.Handled = true;
                break;

            case Key.Enter:
                Accept(Keyboard.Modifiers.HasFlag(ModifierKeys.Alt), Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
                e.Handled = true;
                break;
        }
    }

    private void Select(int index, bool updateText)
    {
        for (int i = 0; i < Items.Count; i++) Items[i].IsSelected = i == index;
        _selected = index;
        if (!updateText || index < 0 || index >= Items.Count) return;

        _suppress = true;
        _box.Text = index == 0 ? _completion : Items[index].FillText;
        if (index == 0 && _completion.Length > _userText.Length)
            _box.Select(_userText.Length, _completion.Length - _userText.Length);
        else
            _box.CaretIndex = _box.Text.Length;
        _suppress = false;
    }

    /// <summary>Opens the highlighted suggestion (or whatever is in the box).</summary>
    public void Accept(bool newTab, bool ctrl = false)
    {
        string? url;
        var text = _box.Text.Trim();
        if (ctrl && !text.Contains(' ') && !text.Contains('.') && text.Length > 0)
            url = $"https://www.{text}.com";                     // Ctrl+Enter: "github" -> www.github.com
        else if (IsOpen && _selected >= 0 && _selected < Items.Count && _rowsFor == _userText)
            url = Items[_selected].Target;
        else
            url = UrlResolver.Resolve(text);

        Log.Write($"Omnibox accept: text='{text}' user='{_userText}' rowsFor='{_rowsFor}' selected={_selected} rows={Items.Count} newTab={newTab} -> {url}");
        Close();
        if (url != null) _navigate(url, newTab);
    }

    public void OpenSuggestion(Suggestion s, bool newTab)
    {
        Close();
        _navigate(s.Target, newTab);
    }

    public void Highlight(Suggestion s) => Select(Items.IndexOf(s), updateText: false);

    private void ForgetSelected()
    {
        if (_selected <= 0 || _selected >= Items.Count) return;
        var row = Items[_selected];
        if (row.Kind != SuggestionKind.History) return;
        HistoryStore.DeleteUrlAsync(row.Target).Forget();
        Items.RemoveAt(_selected);
        Select(Math.Min(_selected, Items.Count - 1), updateText: true);
    }

    public void Close()
    {
        _searchDebounce.Stop();
        _searchCts?.Cancel();
        _popup.IsOpen = false;
        Items.Clear();
        _selected = -1;
        _rowsFor = "";
    }
}
