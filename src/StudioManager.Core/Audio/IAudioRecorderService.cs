namespace StudioManager.Core.Audio;

/// <summary>
/// Microphone capture to MP3 (sound-recorder: NAudio + Lame).
/// </summary>
public interface IAudioRecorderService : IAsyncDisposable
{
    bool IsRecording { get; }
    double Level { get; }
    TimeSpan Elapsed { get; }

    event EventHandler? LevelChanged;
    event EventHandler? ElapsedChanged;

    IReadOnlyList<string> GetInputDevices();
    Task StartAsync(int deviceIndex, string outputFilePath, AudioRecorderOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
