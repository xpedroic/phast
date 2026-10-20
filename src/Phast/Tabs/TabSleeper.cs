using System.Windows.Threading;
using Phast.Core;
using Phast.Session;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>
/// Saves memory and CPU by putting unused background tabs to sleep (Settings > Advanced > Performance):
/// - after "SleepAfterMinutes" the engine suspends the tab (frozen, memory trimmed, instant to resume);
/// - after "DiscardAfterMinutes" the WebView is released completely (the tab reloads when opened);
/// - above "MaxLoadedTabs", the least recently used background tabs are released.
/// Tabs playing audio are never touched. One cheap check every 30 seconds.
/// </summary>
public static class TabSleeper
{
    private static DispatcherTimer? _timer;

    public static void Start()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    public static void Check()
    {
        var s = SettingsService.Current;
        var now = DateTime.Now;
        var loaded = new List<(BrowserTab Tab, TabManager Manager)>();

        foreach (var window in SessionManager.Windows)
        {
            foreach (var tab in window.Tabs.Tabs)
            {
                if (!tab.HasWebView) continue;
                if (tab.IsActive || tab.IsAudible) { loaded.Add((tab, window.Tabs)); continue; }

                var idle = now - tab.LastActivated;
                if (s.DiscardAfterMinutes > 0 && idle.TotalMinutes >= s.DiscardAfterMinutes)
                {
                    if (tab.Sleep(window.Tabs.Host)) Log.Write($"Tab discarded after {idle.TotalMinutes:F0} min: {tab.Url}");
                    continue;
                }
                if (s.SleepAfterMinutes > 0 && idle.TotalMinutes >= s.SleepAfterMinutes && !tab.IsSuspended)
                    tab.TrySuspendAsync().Forget();
                loaded.Add((tab, window.Tabs));
            }
        }

        if (s.MaxLoadedTabs > 0 && loaded.Count > s.MaxLoadedTabs)
        {
            var victims = loaded.Where(x => !x.Tab.IsActive && !x.Tab.IsAudible)
                .OrderBy(x => x.Tab.LastActivated)
                .Take(loaded.Count - s.MaxLoadedTabs);
            foreach (var (tab, manager) in victims) tab.Sleep(manager.Host);
        }
    }
}
