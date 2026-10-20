using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Phast.Bookmarks;
using Phast.Core;
using Phast.Settings;

namespace Phast.Developer;

/// <summary>Exports / imports every setting and the bookmarks to a single JSON file (Settings > Advanced).</summary>
public static class ConfigBackup
{
    private const string Format = "phast-config";

    public static void Export(string path)
    {
        var doc = new JsonObject
        {
            ["format"] = Format,
            ["version"] = 1,
            ["exported"] = DateTime.Now.ToString("s"),
            ["settings"] = SettingsService.ToJson(),
            ["bookmarks"] = JsonSerializer.SerializeToNode(BookmarkStore.Data, JsonFile.Options),
        };
        File.WriteAllText(path, doc.ToJsonString(JsonFile.Options));
    }

    /// <summary>Replaces settings and bookmarks with the file's content. Returns an error message, or null on success.</summary>
    public static string? Import(string path)
    {
        try
        {
            var doc = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
            if (doc?["format"]?.GetValue<string>() != Format) return "El archivo no es una copia de la configuración de Phast.";
            var settings = doc["settings"]?.Deserialize<AppSettings>(JsonFile.Options);
            var bookmarks = doc["bookmarks"]?.Deserialize<BookmarkData>(JsonFile.Options);
            if (settings == null) return "La copia no contiene ajustes.";
            SettingsService.ReplaceAll(settings);
            if (bookmarks != null) BookmarkStore.ReplaceAll(bookmarks);
            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException)
        {
            return "No se pudo leer el archivo: " + ex.Message;
        }
    }
}
