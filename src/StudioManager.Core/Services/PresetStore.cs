using System.Collections.ObjectModel;
using System.Text.Json;
using StudioManager.Core.Camera;

namespace StudioManager.Core.Services;

public sealed class PresetStore
{
    private readonly string _path;
    private readonly List<PtzPreset> _presets = [];

    public PresetStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "presets.json");
        Load();
    }

    public ObservableCollection<PtzPreset> Presets { get; } = [];

    public void Add(PtzPreset preset)
    {
        _presets.Add(preset);
        Presets.Add(preset);
        Save();
    }

    public void Remove(PtzPreset preset)
    {
        _presets.RemoveAll(p => p.Token == preset.Token);
        var existing = Presets.FirstOrDefault(p => p.Token == preset.Token);
        if (existing is not null)
            Presets.Remove(existing);
        Save();
    }

    private void Load()
    {
        Presets.Clear();
        _presets.Clear();
        if (!File.Exists(_path))
            return;

        try
        {
            var json = File.ReadAllText(_path);
            var items = JsonSerializer.Deserialize<List<PtzPreset>>(json) ?? [];
            foreach (var item in items)
            {
                _presets.Add(item);
                Presets.Add(item);
            }
        }
        catch
        {
            // corrupt store — start empty
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_presets, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }
}
