using System.IO;
using System.Text.Json;
using Phast.Bookmarks;
using Phast.Core;
using Phast.History;
using Phast.Session;
using Phast.Settings;
using Xunit;

namespace Phast.Tests;

/// <summary>Everything Phast saves to disk: settings, session, history, bookmarks, per-site zoom.</summary>
public class StorageTests
{
    // ---------------------------------------------------------------- settings

    [Fact]
    public void Settings_OldFileKeepsWorking_NewOptionsGetDefaults()
    {
        var file = Path.Combine(TestSetup.DataDir, "old-settings.json");
        File.WriteAllText(file, """{ "theme": "dark", "searchEngine": "duckduckgo", "somethingRemoved": 42 }""");
        var s = JsonFile.LoadOrDefault<AppSettings>(file);
        Assert.Equal(ThemeMode.Dark, s.Theme);
        Assert.Equal("duckduckgo", s.SearchEngine);
        Assert.True(s.AdblockEnabled);               // option added later: default value
        Assert.True(s.SavePasswords);
        Assert.Equal(5, s.SleepAfterMinutes);
    }

    [Fact]
    public void Settings_BrokenFile_StartsFresh_AndKeepsABackup()
    {
        var file = Path.Combine(TestSetup.DataDir, "broken-settings.json");
        File.WriteAllText(file, "{ this is not json");
        var s = JsonFile.LoadOrDefault<AppSettings>(file);
        Assert.Equal(ThemeMode.System, s.Theme);
        Assert.True(File.Exists(file + ".broken"));
    }

    [Fact]
    public void Settings_SetValue_RejectsUnknownKeysAndWrongTypes()
    {
        SettingsService.ReplaceAll(new AppSettings());
        Assert.True(SettingsService.SetValue("searchEngine", JsonDocument.Parse("\"bing\"").RootElement));
        Assert.Equal("bing", SettingsService.Current.SearchEngine);
        Assert.False(SettingsService.SetValue("notAnOption", JsonDocument.Parse("true").RootElement));
        Assert.False(SettingsService.SetValue("sleepAfterMinutes", JsonDocument.Parse("\"ten\"").RootElement));
        Assert.Equal(5, SettingsService.Current.SleepAfterMinutes);
        SettingsService.ReplaceAll(new AppSettings());
    }

    // ---------------------------------------------------------------- session

    [Fact]
    public void Session_RoundTrip_WithGroups()
    {
        var data = new SessionData
        {
            Windows =
            [
                new SessionWindow
                {
                    Tabs = [new SessionTab { Url = "https://a.example/", Title = "A", GroupId = "g1" }, new SessionTab { Url = "https://b.example/", Title = "B" }],
                    Groups = [new SessionGroup { Id = "g1", Name = "Trabajo", Color = "red", Collapsed = true }],
                    ActiveIndex = 1, Maximized = true,
                },
            ],
        };
        SessionStore.Save(data);
        var back = SessionStore.Load();
        var w = Assert.Single(back.Windows);
        Assert.Equal(2, w.Tabs.Count);
        Assert.Equal("g1", w.Tabs[0].GroupId);
        Assert.Equal("Trabajo", w.Groups[0].Name);
        Assert.True(w.Groups[0].Collapsed);
        Assert.Equal(1, w.ActiveIndex);
        Assert.True(w.Maximized);
    }

    // ---------------------------------------------------------------- history

    [Fact]
    public async Task History_RecordsSearchesAndDeletes()
    {
        await HistoryStore.ClearAsync(DateTime.MinValue);
        await HistoryStore.AddVisitAsync("https://news.example/a", "Noticia de prueba", typed: true);
        await HistoryStore.AddVisitAsync("https://news.example/a", "Noticia de prueba", typed: false);
        await HistoryStore.AddVisitAsync("https://other.example/", "Otra web", typed: false);
        await HistoryStore.AddVisitAsync("https://phast.internal/settings.html", "Ajustes", typed: false); // never recorded

        var found = await HistoryStore.SearchAsync("noticia");
        var entry = Assert.Single(found);
        Assert.Equal(2, entry.VisitCount);
        Assert.Equal(1, entry.TypedCount);

        var visits = await HistoryStore.VisitsAsync(null, 0);
        Assert.Equal(3, visits.Count);
        Assert.DoesNotContain(visits, v => v.Url.Contains("phast.internal"));

        await HistoryStore.DeleteVisitsAsync(visits.Where(v => v.Url.Contains("other")).Select(v => v.Id).ToList());
        Assert.Empty(await HistoryStore.SearchAsync("otra"));        // URL without visits is gone too

        await HistoryStore.ClearAsync(DateTime.Now.AddHours(-1));     // "last hour"
        Assert.Empty(await HistoryStore.VisitsAsync(null, 0));
    }

    // ---------------------------------------------------------------- bookmarks

    [Fact]
    public void Bookmarks_AddMoveRemove_AndSearch()
    {
        BookmarkStore.ReplaceAll(new BookmarkData());
        var folder = BookmarkStore.AddFolder(BookmarkData.BarId, "Noticias");
        var b = BookmarkStore.Add(BookmarkData.BarId, "El País", "https://elpais.com/");
        Assert.True(BookmarkStore.IsBookmarked("https://elpais.com/"));

        BookmarkStore.Move(b.Id, folder.Id, 0);
        Assert.Same(folder, BookmarkStore.ParentOf(b.Id));

        BookmarkStore.Move(folder.Id, folder.Id, 0);                  // a folder can't go inside itself
        Assert.Same(BookmarkStore.Data.Bar, BookmarkStore.ParentOf(folder.Id));

        Assert.Single(BookmarkStore.Search("país"));
        BookmarkStore.Update(b.Id, "El País (diario)", null);
        Assert.Equal("El País (diario)", BookmarkStore.Find(b.Id)!.Title);

        BookmarkStore.Remove(b.Id);
        Assert.False(BookmarkStore.IsBookmarked("https://elpais.com/"));
    }

    [Fact]
    public async Task Bookmarks_ImportFromChromiumProfile()
    {
        BookmarkStore.ReplaceAll(new BookmarkData());
        var profile = Path.Combine(TestSetup.DataDir, "FakeChrome", "Default");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, "Bookmarks"), """
            { "roots": {
                "bookmark_bar": { "type": "folder", "name": "Bookmarks bar", "children": [
                    { "type": "url", "name": "GitHub", "url": "https://github.com/" },
                    { "type": "folder", "name": "Dev", "children": [ { "type": "url", "name": "MDN", "url": "https://developer.mozilla.org/" } ] } ] },
                "other": { "type": "folder", "name": "Other", "children": [ { "type": "url", "name": "Otro", "url": "https://other.example/" } ] },
                "synced": { "type": "folder", "name": "Mobile", "children": [] } } }
            """);
        var count = await BrowserImporter.ImportAsync(new ImportSource("Chrome", profile, "Persona 1", Path.Combine(profile, "Bookmarks"), 3));

        Assert.Equal(3, count);
        Assert.Equal(2, BookmarkStore.Data.Bar.Children!.Count);      // empty bar: Chrome's bar items go straight onto it
        Assert.True(BookmarkStore.IsBookmarked("https://developer.mozilla.org/"));
        var imported = Assert.Single(BookmarkStore.Data.Other.Children!);
        Assert.Equal("Importado de Chrome", imported.Title);
    }

    // ---------------------------------------------------------------- zoom per site

    [Fact]
    public void Zoom_RememberedPerSite_DefaultIs100()
    {
        ZoomMemory.Set("es.wikipedia.org", 1.25);
        Assert.Equal(1.25, ZoomMemory.Get("es.wikipedia.org"));
        Assert.Equal(1, ZoomMemory.Get("github.com"));
        ZoomMemory.Set("es.wikipedia.org", 1);                        // back to 100 % forgets it
        Assert.Equal(1, ZoomMemory.Get("es.wikipedia.org"));
        ZoomMemory.Set(InternalPages.Host, 2);                        // internal pages are never zoom-remembered
        Assert.Equal(1, ZoomMemory.Get(InternalPages.Host));
    }
}
