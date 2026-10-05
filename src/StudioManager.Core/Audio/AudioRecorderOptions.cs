namespace StudioManager.Core.Audio;

/// <summary>
/// Settings migrated from sound-recorder appsettings.
/// </summary>
public sealed class AudioRecorderOptions
{
    public string SavePath { get; set; } = string.Empty;
    public bool AutoStartRecording { get; set; }
    public bool CloseAfterSave { get; set; }
    public int SampleRateHz { get; set; } = 44100;
    public int Mp3BitrateKbps { get; set; } = 128;
}
