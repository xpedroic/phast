using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Phast.Adblock;
using Phast.Core;
using Phast.Settings;

namespace Phast.Tabs;

/// <summary>
/// Element hiding ("hide ad gaps") for one tab. A small script (Pages/scripts/cosmetic.js) reports the class
/// names and ids used by the page; we answer with CSS that hides the ad containers among them.
/// </summary>
public sealed partial class BrowserTab
{
    private static string? _cosmeticScript;
    private string? _cosmeticScriptId;
    private HashSet<string> _cosmeticSent = new(StringComparer.Ordinal);
    private bool _cosmeticBaseSent;

    private async Task InitCosmeticAsync(CoreWebView2 core)
    {
        _cosmeticScriptId = null;
        core.NavigationStarting += (_, e) =>
        {
            if (e.IsRedirected) return;
            _cosmeticSent = new HashSet<string>(StringComparer.Ordinal);
            _cosmeticBaseSent = false;
        };
        await RefreshCosmeticScriptAsync();
    }

    /// <summary>Registers or removes the reporting script according to the "hide ad gaps" setting.</summary>
    public async Task RefreshCosmeticScriptAsync()
    {
        var core = Core;
        if (core == null) return;
        var wanted = SettingsService.Current.AdblockEnabled && SettingsService.Current.AdblockCosmetic;
        if (wanted && _cosmeticScriptId == null)
        {
            _cosmeticScriptId = "";
            _cosmeticScript ??= File.ReadAllText(Path.Combine(AppPaths.Pages, "scripts", "cosmetic.js"));
            _cosmeticScriptId = await core.AddScriptToExecuteOnDocumentCreatedAsync(_cosmeticScript);
        }
        else if (!wanted && !string.IsNullOrEmpty(_cosmeticScriptId))
        {
            core.RemoveScriptToExecuteOnDocumentCreated(_cosmeticScriptId);
            _cosmeticScriptId = null;
        }
    }

    /// <summary>Handles {"phastCosmetic": [".class", "#id", ...]} sent by cosmetic.js. Returns true if it was such a message.</summary>
    private bool TryHandleCosmeticMessage(string json)
    {
        if (!json.Contains("\"phastCosmetic\"", StringComparison.Ordinal)) return false;
        var engine = AdblockService.Engine;
        if (!_blockingActive || engine == null || Core == null || !SettingsService.Current.AdblockCosmetic) return true;

        List<string> tokens;
        try
        {
            using var doc = JsonDocument.Parse(json);
            tokens = doc.RootElement.GetProperty("phastCosmetic").EnumerateArray()
                .Where(t => t.ValueKind == JsonValueKind.String)
                .Select(t => t.GetString()!)
                .Where(t => t.Length is > 1 and < 200)
                .Take(8000)
                .ToList();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return true; // malformed: ignore
        }

        var css = engine.GenericCssFor(tokens, _docHost, _cosmeticSent);
        if (!_cosmeticBaseSent)
        {
            _cosmeticBaseSent = true;
            css = engine.CosmeticCss(_docHost) + css;
        }
        if (css.Length == 0) return true;

        var script = $$"""
            (() => {
              let s = document.getElementById('phast-cosmetic');
              if (!s) { s = document.createElement('style'); s.id = 'phast-cosmetic'; (document.head || document.documentElement).appendChild(s); }
              s.textContent += {{JsonSerializer.Serialize(css)}};
            })();
            """;
        _ = Core.ExecuteScriptAsync(script);
        return true;
    }
}
