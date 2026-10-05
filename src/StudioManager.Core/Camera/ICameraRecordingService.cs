namespace StudioManager.Core.Camera;

/// <summary>
/// Record / snapshot via FFmpeg against the camera RTSP URL (HCP: StartFFmpegRecording / CaptureSnapshot).
/// </summary>
public interface ICameraRecordingService
{
    bool IsRecording { get; }

    Task StartRecordingAsync(CameraConnectionOptions options, string outputFilePath, CancellationToken cancellationToken = default);
    Task StopRecordingAsync(CancellationToken cancellationToken = default);
    Task CaptureSnapshotAsync(CameraConnectionOptions options, string outputFilePath, CancellationToken cancellationToken = default);
}
