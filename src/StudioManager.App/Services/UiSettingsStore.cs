using System.IO;
using System.Text.Json;

namespace StudioManager.App.Services;

/// <summary>
/// Persists user UI preferences (culture) across sessions.
/// </summary>
public sealed class UiSettingsStore
{
    private readonly string _path;

    public UiSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudioManager",
            "ui-settings.json");
    }

    public string? LoadCulture()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<UiSettingsData>(json);
            return string.IsNullOrWhiteSpace(data?.Culture) ? null : data.Culture;
        }
        catch
        {
            return null;
        }
    }

    public void SaveCulture(string culture)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var data = new UiSettingsData { Culture = culture };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // ignore persistence failures
        }
    }

    private sealed class UiSettingsData
    {
        public string Culture { get; set; } = "fa-IR";
    }
}
