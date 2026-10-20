using Phast.Core;

namespace Phast.Session;

public sealed class SessionTab
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    /// <summary>Base64 PNG favicon so restored (not yet loaded) tabs still show their icon.</summary>
    public string? Favicon { get; set; }
    /// <summary>Id of the tab group (see <see cref="SessionWindow.Groups"/>), if any.</summary>
    public string? GroupId { get; set; }
}

public sealed class SessionGroup
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Color { get; set; } = "blue";
    public bool Collapsed { get; set; }
    public string? PageGroupId { get; set; }
}

public sealed class SessionWindow
{
    public List<SessionTab> Tabs { get; set; } = [];
    public List<SessionGroup> Groups { get; set; } = [];
    public int ActiveIndex { get; set; }
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1280;
    public double Height { get; set; } = 800;
    public bool Maximized { get; set; }
}

public sealed class SessionData
{
    public List<SessionWindow> Windows { get; set; } = [];
}

/// <summary>Reads and writes session.json (open windows and their tabs).</summary>
public static class SessionStore
{
    public static SessionData Load() => JsonFile.LoadOrDefault<SessionData>(AppPaths.SessionFile);

    public static void Save(SessionData data)
    {
        try { JsonFile.Save(AppPaths.SessionFile, data); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Session save failed: {ex.Message}"); }
    }
}
