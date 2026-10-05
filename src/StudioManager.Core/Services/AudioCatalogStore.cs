using System.Text.Json;
using StudioManager.Core.Audio;

namespace StudioManager.Core.Services;

public sealed class AudioCatalogStore
{
    private readonly string _path;

    public AudioCatalogStore(string? path = null)
    {
        _path = path ?? Path.Combine(AppContext.BaseDirectory, "audio-catalog.json");
        Catalog = Load();
    }

    public AudioCatalog Catalog { get; private set; }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Catalog, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    private AudioCatalog Load()
    {
        if (!File.Exists(_path))
            return new AudioCatalog();

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AudioCatalog>(json) ?? new AudioCatalog();
        }
        catch
        {
            return new AudioCatalog();
        }
    }
}
