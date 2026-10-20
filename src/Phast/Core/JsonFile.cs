using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Phast.Core;

/// <summary>
/// Small helper to load/save JSON files atomically (write to temp file, then replace),
/// so a crash in the middle of a save never corrupts user data.
/// </summary>
public static class JsonFile
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Window bounds can be NaN/Infinity (e.g. a window that was never in normal state): must not break saving.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static T LoadOrDefault<T>(string path) where T : new()
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? new T();
        }
        catch (Exception ex)
        {
            // A broken file must never prevent the browser from starting: keep a backup and start fresh.
            System.Diagnostics.Debug.WriteLine($"Failed to read {path}: {ex.Message}");
            try { File.Copy(path, path + ".broken", overwrite: true); } catch { /* best effort */ }
        }
        return new T();
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
