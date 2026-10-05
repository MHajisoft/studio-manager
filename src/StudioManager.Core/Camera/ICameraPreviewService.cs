namespace StudioManager.Core.Camera;

/// <summary>
/// Live RTSP decode/preview surface (HCP: TDecodeRTSPThread + Image preview).
/// </summary>
public interface ICameraPreviewService : IAsyncDisposable
{
    bool IsStreaming { get; }

    /// <summary>Raised on a background thread when a BGR24 frame is ready.</summary>
    event EventHandler<CameraFrameEventArgs>? FrameReceived;

    Task StartAsync(CameraConnectionOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class CameraFrameEventArgs : EventArgs
{
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int Stride { get; init; }

    /// <summary>BGR24 pixel buffer (owned by the service until the next frame).</summary>
    public required ReadOnlyMemory<byte> Bgr24Data { get; init; }
}
