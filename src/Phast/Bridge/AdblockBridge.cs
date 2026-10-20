using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Phast.Adblock;
using Phast.Settings;
using Phast.Tabs;

namespace Phast.Bridge;

/// <summary>"adblock.*" messages from phast://settings (privacy section).</summary>
public static class AdblockBridge
{
    public static void Initialize()
    {
        // Status changes come from background threads (downloads, rebuilds).
        AdblockService.StatusChanged += () =>
            Application.Current?.Dispatcher.BeginInvoke(() => PageBridge.Broadcast(StatusMessage(), "settings.html"));
        // The allowlist can change from the shield bubble while Settings is open.
        SettingsService.Changed += () => PageBridge.Broadcast(StatusMessage(), "settings.html");
    }

    public static void Handle(BrowserTab tab, string type, JsonElement msg)
    {
        switch (type)
        {
            case "adblock.status":
                tab.PostMessage(StatusMessage());
                break;
            case "adblock.update":
                _ = Task.Run(() => AdblockService.UpdateAsync(force: true));
                break;
            case "adblock.setList":
                var id = msg.GetProperty("id").GetString() ?? "";
                var on = msg.GetProperty("enabled").GetBoolean();
                if (FilterLists.Get(id) == null) break;
                SettingsService.Update(s =>
                {
                    s.FilterLists.Remove(id);
                    if (on) s.FilterLists.Add(id);
                });
                AdblockService.OnListsChanged();
                break;
            case "adblock.unallow":
                var host = msg.GetProperty("host").GetString() ?? "";
                SettingsService.Update(s => s.AdblockAllowlist.Remove(host));
                tab.PostMessage(StatusMessage());
                break;
        }
    }

    public static string StatusMessage()
    {
        var engine = AdblockService.Engine;
        var enabled = SettingsService.Current.FilterLists;
        return new JsonObject
        {
            ["type"] = "adblockStatus",
            ["ready"] = engine != null,
            ["updating"] = AdblockService.Updating,
            ["networkRules"] = engine?.NetworkRuleCount ?? 0,
            ["cosmeticRules"] = engine?.CosmeticRuleCount ?? 0,
            ["buildMs"] = (int)AdblockService.BuildTime.TotalMilliseconds,
            ["lists"] = new JsonArray(FilterLists.BuiltIn.Select(l => (JsonNode)new JsonObject
            {
                ["id"] = l.Id,
                ["name"] = l.Name,
                ["description"] = l.Description,
                ["enabled"] = enabled.Contains(l.Id),
                ["updated"] = FilterLists.LastUpdated(l.Id)?.ToString("d/M/yyyy HH:mm"),
            }).ToArray()),
            ["allowlist"] = new JsonArray(SettingsService.Current.AdblockAllowlist.Select(h => (JsonNode)h!).ToArray()),
        }.ToJsonString();
    }
}
