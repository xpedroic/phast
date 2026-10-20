using Phast.Core;

namespace Phast.Omnibox;

public enum SuggestionKind
{
    /// <summary>Go to a typed / autocompleted address.</summary>
    Url,
    /// <summary>Search the web for a query.</summary>
    Search,
    /// <summary>A page from the browsing history.</summary>
    History,
    /// <summary>A bookmarked page.</summary>
    Bookmark,
}

/// <summary>One row of the address bar dropdown.</summary>
public sealed class Suggestion : ObservableObject
{
    private bool _isSelected;

    public required SuggestionKind Kind { get; init; }
    /// <summary>Main text (page title or search query).</summary>
    public required string Text { get; init; }
    /// <summary>Secondary text (URL, or "Búsqueda de Google").</summary>
    public string? Detail { get; init; }
    /// <summary>URL to open when the suggestion is chosen.</summary>
    public required string Target { get; init; }
    /// <summary>Text shown in the address bar while the row is highlighted with the arrow keys.</summary>
    public required string FillText { get; init; }

    /// <summary>The detail is a URL (shown in the accent color, like Chrome).</summary>
    public bool DetailIsUrl => Kind is SuggestionKind.History or SuggestionKind.Bookmark or SuggestionKind.Url;

    public string Glyph => Kind switch
    {
        SuggestionKind.Search => "",   // magnifier
        SuggestionKind.History => "",  // clock
        SuggestionKind.Bookmark => "", // filled star
        _ => "",                       // globe
    };

    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }
}
