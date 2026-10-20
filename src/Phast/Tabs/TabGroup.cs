using System.Windows.Media;
using Phast.Core;
using Phast.Themes;

namespace Phast.Tabs;

/// <summary>Chrome's tab group colors, in a light-theme and a dark-theme variant.</summary>
public static class GroupColors
{
    public static readonly IReadOnlyList<(string Id, string Name, string Light, string Dark)> All =
    [
        ("grey", "Gris", "#5F6368", "#DADCE0"),
        ("blue", "Azul", "#1A73E8", "#8AB4F8"),
        ("red", "Rojo", "#D93025", "#F28B82"),
        ("yellow", "Amarillo", "#E8A600", "#FDD663"),
        ("green", "Verde", "#188038", "#81C995"),
        ("pink", "Rosa", "#D01884", "#FF8BCB"),
        ("purple", "Morado", "#A142F4", "#C58AF9"),
        ("cyan", "Cian", "#007B83", "#78D9EC"),
        ("orange", "Naranja", "#FA903E", "#FCAD70"),
    ];

    public static Color ColorOf(string id)
    {
        var c = All.FirstOrDefault(x => x.Id == id);
        if (c.Id == null) c = All[1];
        return (Color)ColorConverter.ConvertFromString(ThemeManager.IsDark ? c.Dark : c.Light);
    }

    /// <summary>Picks the next color not used yet (like Chrome does for new groups).</summary>
    public static string Next(IEnumerable<string> used)
    {
        var usedSet = used.ToHashSet();
        return All.Skip(1).Select(c => c.Id).FirstOrDefault(id => !usedSet.Contains(id)) ?? "blue";
    }
}

/// <summary>A group of tabs in the tab strip: colored label, collapsible, optionally linked to a saved page group.</summary>
public sealed class TabGroup : ObservableObject
{
    private string _name;
    private string _color;
    private bool _isCollapsed;

    public TabGroup(string name, string color, string? pageGroupId = null)
    {
        _name = name;
        _color = color;
        PageGroupId = pageGroupId;
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Saved page group (Settings) this tab group was opened from, if any.</summary>
    public string? PageGroupId { get; set; }

    public string Name { get => _name; set { if (Set(ref _name, value)) OnPropertyChanged(nameof(Label)); } }
    public string Color { get => _color; set { if (Set(ref _color, value)) OnThemeChanged(); } }
    public bool IsCollapsed { get => _isCollapsed; set { if (Set(ref _isCollapsed, value)) OnPropertyChanged(nameof(Label)); } }

    /// <summary>Text of the chip: the name, or "+3"-style count when collapsed and unnamed.</summary>
    public string Label => Name.Length > 0 ? Name : "";

    public Brush Brush => Frozen(GroupColors.ColorOf(_color));
    /// <summary>Chip text color: white on light theme colors, dark on dark theme colors (like Chrome).</summary>
    public Brush Foreground => Frozen(ThemeManager.IsDark ? Colors.Black : Colors.White);

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private void OnThemeChanged()
    {
        OnPropertyChanged(nameof(Brush));
        OnPropertyChanged(nameof(Foreground));
    }

    public void Detach() => ThemeManager.ThemeChanged -= OnThemeChanged;
}
