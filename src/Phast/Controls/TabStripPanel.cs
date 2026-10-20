using System.Windows;
using System.Windows.Controls;
using Phast.Tabs;

namespace Phast.Controls;

/// <summary>
/// Lays tabs out horizontally like Chrome: every tab gets the same width, up to <see cref="MaxTabWidth"/>,
/// shrinking evenly (down to <see cref="MinTabWidth"/>) as more tabs are opened.
/// Tab group labels take just the width they need; hidden (collapsed) items take none.
/// </summary>
public sealed class TabStripPanel : Panel
{
    public double MaxTabWidth { get; set; } = 240;
    public double MinTabWidth { get; set; } = 44;

    /// <summary>Width each tab currently gets.</summary>
    public double TabWidth { get; private set; }

    private static bool IsGroupLabel(UIElement child) => (child as FrameworkElement)?.DataContext is TabGroup;

    protected override Size MeasureOverride(Size available)
    {
        double labels = 0, height = 0;
        var tabs = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) continue;
            if (IsGroupLabel(child))
            {
                child.Measure(new Size(double.PositiveInfinity, available.Height));
                labels += child.DesiredSize.Width;
                height = Math.Max(height, child.DesiredSize.Height);
            }
            else tabs++;
        }

        var forTabs = double.IsInfinity(available.Width) ? MaxTabWidth * tabs : Math.Max(0, available.Width - labels);
        TabWidth = tabs == 0 ? 0 : Math.Clamp(forTabs / tabs, MinTabWidth, MaxTabWidth);

        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed || IsGroupLabel(child)) continue;
            child.Measure(new Size(TabWidth, available.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(labels + TabWidth * tabs, height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed) { child.Arrange(new Rect(x, 0, 0, 0)); continue; }
            var width = IsGroupLabel(child) ? child.DesiredSize.Width : TabWidth;
            child.Arrange(new Rect(x, 0, width, final.Height));
            x += width;
        }
        return final;
    }
}
