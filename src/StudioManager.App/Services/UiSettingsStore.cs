using System.IO;
using System.Text.Json;
using StudioManager.Core.Configuration;

namespace StudioManager.App.Services;

/// <summary>
/// Persists user preferences (culture + save paths) across sessions.
/// </summary>
public sealed class UiSettingsStore
{
    private readonly string _path;
    private UiSettingsData _data = new();

    public UiSettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudioManager",
            "ui-settings.json");
        _data = Load() ?? new UiSettingsData();
    }

    public string? Culture => string.IsNullOrWhiteSpace(_data.Culture) ? null : _data.Culture;

    public void ApplyTo(StudioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(_data.Culture))
            options.Ui.Culture = _data.Culture!;

        if (!string.IsNullOrWhiteSpace(_data.VideoFolder))
            options.Camera.VideoFolder = _data.VideoFolder!;

        if (!string.IsNullOrWhiteSpace(_data.SnapshotFolder))
            options.Camera.SnapshotFolder = _data.SnapshotFolder!;

        if (!string.IsNullOrWhiteSpace(_data.AudioSavePath))
            options.Audio.SavePath = _data.AudioSavePath!;

        if (_data.MediaFolders is { Count: > 0 })
            options.MediaFolders = [.. _data.MediaFolders];
    }

    public void SaveCulture(string culture)
    {
        _data.Culture = culture;
        Persist();
    }

    public void SavePaths(string videoFolder, string snapshotFolder, string audioSavePath, IEnumerable<string> mediaFolders)
    {
        _data.VideoFolder = videoFolder;
        _data.SnapshotFolder = snapshotFolder;
        _data.AudioSavePath = audioSavePath;
        _data.MediaFolders = mediaFolders
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Persist();
    }

    private UiSettingsData? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<UiSettingsData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // ignore persistence failures
        }
    }

    private sealed class UiSettingsData
    {
        public string? Culture { get; set; }
        public string? VideoFolder { get; set; }
        public string? SnapshotFolder { get; set; }
        public string? AudioSavePath { get; set; }
        public List<string>? MediaFolders { get; set; }
    }
}
