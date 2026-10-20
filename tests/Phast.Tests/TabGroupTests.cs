using System.Windows.Controls;
using Phast.Tabs;
using Xunit;

namespace Phast.Tests;

/// <summary>Tab groups: tabs of a group stay together, the strip shows one label per group, drag in/out works.</summary>
public class TabGroupTests
{
    // Tabs are opened without activating them, so no web engine is ever started by these tests.
    private static TabManager Manager(int tabs)
    {
        var m = new TabManager(new Grid());
        for (int i = 0; i < tabs; i++) m.Open($"https://site{i}.example/", activate: false);
        return m;
    }

    private static string Order(TabManager m) => string.Join(",", m.Tabs.Select(t => t.Url[8..13]));

    [Fact]
    public void Strip_ShowsOneLabelBeforeEachGroup() => TestSetup.OnSta(() =>
    {
        var m = Manager(4);
        var g = m.CreateGroup("Trabajo");
        m.SetGroup(m.Tabs[1], g);
        m.SetGroup(m.Tabs[2], g);

        Assert.Equal(5, m.StripItems.Count);              // 4 tabs + 1 label
        Assert.Same(g, m.StripItems[1]);                   // label right before its first tab
        Assert.Same(m.Tabs[1], m.StripItems[2]);
        Assert.Single(m.Groups);
    });

    [Fact]
    public void AddingToGroup_MovesTabNextToTheGroup() => TestSetup.OnSta(() =>
    {
        var m = Manager(5);
        var g = m.CreateGroup("Ocio");
        m.SetGroup(m.Tabs[0], g);
        m.SetGroup(m.Tabs[4], g);                           // far away tab joins the group
        Assert.Equal("site0,site4,site1,site2,site3", Order(m));
        Assert.All(m.Tabs.Take(2), t => Assert.Same(g, t.Group));
    });

    [Fact]
    public void LeavingGroup_StepsOutAfterTheGroup() => TestSetup.OnSta(() =>
    {
        var m = Manager(4);
        var g = m.CreateGroup("G");
        foreach (var t in m.Tabs.Take(3).ToList()) m.SetGroup(t, g);
        m.SetGroup(m.Tabs[0], null);                        // first tab of the group leaves it
        Assert.Equal("site1,site2,site0,site3", Order(m));  // group stays in one piece
        Assert.Null(m.Tabs[2].Group);
    });

    [Fact]
    public void DraggingIntoAGroup_Joins_AndOut_Leaves() => TestSetup.OnSta(() =>
    {
        var m = Manager(4);
        var g = m.CreateGroup("G");
        m.SetGroup(m.Tabs[1], g);
        m.SetGroup(m.Tabs[2], g);
        var outsider = m.Tabs[3];

        m.Move(3, 2);                                       // dropped between two tabs of the group
        Assert.Same(g, outsider.Group);

        m.Move(m.Tabs.IndexOf(outsider), 0);                // dragged to the start, outside the group
        Assert.Null(outsider.Group);
    });

    [Fact]
    public void Ungroup_And_CloseGroup() => TestSetup.OnSta(() =>
    {
        var m = Manager(4);
        var a = m.CreateGroup("A");
        m.SetGroup(m.Tabs[0], a);
        m.SetGroup(m.Tabs[1], a);
        m.Ungroup(a);
        Assert.All(m.Tabs, t => Assert.Null(t.Group));
        Assert.Equal(4, m.StripItems.Count);

        var b = m.CreateGroup("B");
        m.SetGroup(m.Tabs[2], b);
        m.SetGroup(m.Tabs[3], b);
        m.CloseGroup(b);
        Assert.Equal(2, m.Tabs.Count);
        Assert.Empty(m.Groups);
    });

    [Fact]
    public void NewGroups_GetDifferentColors() => TestSetup.OnSta(() =>
    {
        var m = Manager(3);
        var a = m.CreateGroup("A");
        m.SetGroup(m.Tabs[0], a);
        var b = m.CreateGroup("B");
        Assert.NotEqual(a.Color, b.Color);
    });

    [Fact]
    public void ClosedTabs_AreRemembered_NewTabPagesAreNot() => TestSetup.OnSta(() =>
    {
        var m = Manager(3);
        m.Close(m.Tabs[1]);
        Assert.Equal("site0,site2", Order(m));
        Assert.True(m.HasClosedTabs);       // Ctrl+Shift+T can bring it back (reopening starts the engine: not tested here)

        var blank = new TabManager(new Grid());
        blank.Open(null, activate: false);  // "new tab" page
        blank.Open("https://a.example/", activate: false);
        blank.Close(blank.Tabs[0]);
        Assert.False(blank.HasClosedTabs);  // closing an empty new tab is not worth remembering
    });
}
