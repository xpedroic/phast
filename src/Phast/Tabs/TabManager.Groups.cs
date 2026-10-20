using System.Collections.ObjectModel;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>
/// Tab groups: what the tab strip shows (group labels + tabs), creating/collapsing/ungrouping groups,
/// and keeping the tabs of a group next to each other.
/// </summary>
public sealed partial class TabManager
{
    /// <summary>Items of the tab strip: a <see cref="TabGroup"/> label before the first tab of each group, then tabs.</summary>
    public ObservableCollection<object> StripItems { get; } = new();

    /// <summary>Groups currently present in this window (in strip order).</summary>
    public IEnumerable<TabGroup> Groups => Tabs.Select(t => t.Group).OfType<TabGroup>().Distinct();

    /// <summary>Rebuilds <see cref="StripItems"/> with the fewest changes (so tab visuals are kept, not recreated).</summary>
    public void SyncStrip()
    {
        var desired = new List<object>(Tabs.Count + 4);
        TabGroup? previous = null;
        foreach (var tab in Tabs)
        {
            if (tab.Group != null && tab.Group != previous) desired.Add(tab.Group);
            desired.Add(tab);
            previous = tab.Group;
        }

        for (int i = 0; i < desired.Count; i++)
        {
            if (i < StripItems.Count && ReferenceEquals(StripItems[i], desired[i])) continue;
            var existing = -1;
            for (int j = i + 1; j < StripItems.Count; j++)
                if (ReferenceEquals(StripItems[j], desired[i])) { existing = j; break; }
            if (existing >= 0) StripItems.Move(existing, i);
            else StripItems.Insert(i, desired[i]);
        }
        while (StripItems.Count > desired.Count) StripItems.RemoveAt(StripItems.Count - 1);

        // Groups without tabs are gone.
        foreach (var g in _knownGroups.Except(Groups).ToList()) { g.Detach(); _knownGroups.Remove(g); }
        foreach (var g in Groups) _knownGroups.Add(g);
    }

    private readonly HashSet<TabGroup> _knownGroups = [];

    public TabGroup CreateGroup(string name, string? color = null, string? pageGroupId = null) =>
        new(name, color ?? GroupColors.Next(Groups.Select(g => g.Color)), pageGroupId);

    /// <summary>Puts a tab into a group (moving it next to the group's other tabs) or out of any group (null).</summary>
    public void SetGroup(BrowserTab tab, TabGroup? group)
    {
        if (tab.Group == group) return;
        var old = tab.Group;
        tab.Group = group;

        var from = Tabs.IndexOf(tab);
        if (group != null)
        {
            var members = Tabs.Where(t => t.Group == group && t != tab).ToList();
            if (members.Count > 0)
            {
                var last = Tabs.IndexOf(members[^1]);
                Tabs.Move(from, from > last ? last + 1 : last);
            }
        }
        else if (old != null)
        {
            // Leaving a group: step out after its last tab so the group stays in one piece.
            var members = Tabs.Where(t => t.Group == old).ToList();
            if (members.Count > 0)
            {
                var last = Tabs.IndexOf(members[^1]);
                if (last > from) Tabs.Move(from, last);
            }
        }
        SyncStrip();
        SessionDirty?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>After a drag: join the group the tab was dropped into, or leave its group if dropped outside it.</summary>
    private void FixGroupAfterMove(BrowserTab tab)
    {
        var i = Tabs.IndexOf(tab);
        var left = i > 0 ? Tabs[i - 1] : null;
        var right = i + 1 < Tabs.Count ? Tabs[i + 1] : null;
        if (left?.Group != null && left.Group == right?.Group) tab.Group = left.Group;            // dropped inside a group
        else if (tab.Group != null && left?.Group != tab.Group && right?.Group != tab.Group
                 && Tabs.Any(t => t != tab && t.Group == tab.Group)) tab.Group = null;          // dragged out of its group
        SyncStrip();
    }

    public void SetCollapsed(TabGroup group, bool collapsed)
    {
        if (group.IsCollapsed == collapsed) return;
        if (collapsed && Active?.Group == group)
        {
            // The active tab can't hide: switch to the nearest tab outside the group (or don't collapse).
            var i = Tabs.IndexOf(Active);
            var outside = Tabs.Skip(i).Concat(Tabs.Take(i).Reverse()).FirstOrDefault(t => t.Group != group);
            if (outside == null) return;
            group.IsCollapsed = true;
            Activate(outside);
        }
        group.IsCollapsed = collapsed;
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);
    }

    public void Ungroup(TabGroup group)
    {
        group.IsCollapsed = false;
        foreach (var t in Tabs.Where(t => t.Group == group)) t.Group = null;
        SyncStrip();
        SessionDirty?.Invoke(this, EventArgs.Empty);
    }

    public void CloseGroup(TabGroup group)
    {
        foreach (var t in Tabs.Where(t => t.Group == group).ToList()) Close(t);
    }

    /// <summary>Opens a saved page group as a tab group (or shows it if it is already open in this window).</summary>
    public void OpenPageGroup(PageGroup pageGroup)
    {
        var existing = Groups.FirstOrDefault(g => g.PageGroupId == pageGroup.Id);
        if (existing != null)
        {
            SetCollapsed(existing, false);
            Activate(Tabs.First(t => t.Group == existing));
            return;
        }
        if (pageGroup.Urls.Count == 0) return;

        var group = CreateGroup(pageGroup.Name, pageGroup.Color, pageGroup.Id);
        BrowserTab? first = null;
        foreach (var url in pageGroup.Urls)
        {
            // Only the first page loads now; the others wait until they are opened (lazy tabs).
            var resolved = Core.UrlResolver.Resolve(url) ?? url;
            var tab = new BrowserTab(resolved) { Group = group };
            Attach(tab);
            Tabs.Add(tab);
            first ??= tab;
        }
        Activate(first!);
        UpdateSeparators();
        SessionDirty?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates a new group containing just this tab (tab context menu).</summary>
    public TabGroup GroupTab(BrowserTab tab)
    {
        var group = CreateGroup("");
        SetGroup(tab, group);
        return group;
    }
}
