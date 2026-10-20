using System.Text.Json;
using System.Text.Json.Nodes;
using Phast.Core;

namespace Phast.Settings;

/// <summary>
/// Owns the single <see cref="AppSettings"/> instance, loads it at startup and saves it on change.
/// </summary>
public static class SettingsService
{
    public static AppSettings Current { get; private set; } = new();

    /// <summary>Raised (on the UI thread) after settings were modified and saved.</summary>
    public static event Action? Changed;

    public static void Load() => Current = JsonFile.LoadOrDefault<AppSettings>(AppPaths.SettingsFile);

    /// <summary>Applies a modification, persists it and notifies listeners.</summary>
    public static void Update(Action<AppSettings> change)
    {
        change(Current);
        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// Sets one option by its JSON name (e.g. "searchEngine") — used by the settings page.
    /// Unknown keys or values of the wrong type are rejected, so a page can never corrupt the settings.
    /// </summary>
    public static bool SetValue(string key, JsonElement value)
    {
        var node = JsonSerializer.SerializeToNode(Current, JsonFile.Options)!.AsObject();
        if (!node.ContainsKey(key)) return false;
        node[key] = JsonNode.Parse(value.GetRawText());
        try
        {
            var updated = node.Deserialize<AppSettings>(JsonFile.Options);
            if (updated == null) return false;
            Current = updated;
        }
        catch (JsonException)
        {
            return false;
        }
        Save();
        Changed?.Invoke();
        return true;
    }

    /// <summary>Settings as a JSON object (camelCase), for the settings page.</summary>
    public static JsonNode ToJson() => JsonSerializer.SerializeToNode(Current, JsonFile.Options)!;

    public static void Save() => JsonFile.Save(AppPaths.SettingsFile, Current);
}
