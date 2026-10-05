using StudioManager.Core.Audio;
using StudioManager.Core.Camera;

namespace StudioManager.Core.Configuration;

/// <summary>
/// Root application settings loaded from appsettings.json.
/// </summary>
public sealed class StudioOptions
{
    public const string SectionName = "Studio";

    public CameraConnectionOptions Camera { get; set; } = new() { Host = "196.254.67.11" };
    public AudioRecorderOptions Audio { get; set; } = new();
    public List<string> MediaFolders { get; set; } = [];
}
