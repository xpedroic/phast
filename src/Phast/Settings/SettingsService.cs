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

    public static void Save() => JsonFile.Save(AppPaths.SettingsFile, Current);
}
