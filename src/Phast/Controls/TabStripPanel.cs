using System.Windows;
using System.Windows.Controls;

namespace Phast.Controls;

/// <summary>
/// Lays tabs out horizontally like Chrome: every tab gets the same width, up to <see cref="MaxTabWidth"/>,
/// shrinking evenly (down to <see cref="MinTabWidth"/>) as more tabs are opened.
/// Tabs overlap by <see cref="Overlap"/> so their curved "feet" blend into each other.
/// </summary>
public sealed class TabStripPanel : Panel
{
    public double MaxTabWidth { get; set; } = 240;
    public double MinTabWidth { get; set; } = 44;
    public double Overlap { get; set; } = 0;

    /// <summary>Width each tab currently gets; used by drag-and-drop to find the drop index.</summary>
    public double TabWidth { get; private set; }

    protected override Size MeasureOverride(Size available)
    {
        var n = InternalChildren.Count;
        if (n == 0) return new Size(0, 0);

        var width = double.IsInfinity(available.Width) ? MaxTabWidth : (available.Width + Overlap * (n - 1)) / n;
        TabWidth = Math.Clamp(width, MinTabWidth, MaxTabWidth);

        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(new Size(TabWidth, available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(TabWidth * n - Overlap * (n - 1), height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Arrange(new Rect(x, 0, TabWidth, final.Height));
            x += TabWidth - Overlap;
        }
        return final;
    }
}
