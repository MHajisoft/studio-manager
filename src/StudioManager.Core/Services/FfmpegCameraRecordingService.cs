using System.Diagnostics;
using StudioManager.Core.Camera;

namespace StudioManager.Core.Services;

public sealed class FfmpegCameraRecordingService : ICameraRecordingService, IDisposable
{
    private Process? _process;
    private StreamWriter? _stdin;

    public bool IsRecording => _process is { HasExited: false };

    public Task StartRecordingAsync(CameraConnectionOptions options, string outputFilePath, CancellationToken cancellationToken = default)
    {
        if (IsRecording)
            throw new InvalidOperationException("Recording is already in progress.");

        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-rtsp_transport tcp -i \"{options.RtspUrl}\" -c:v copy -c:a aac -b:a 128k \"{outputFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg.");
        _stdin = _process.StandardInput;
        return Task.CompletedTask;
    }

    public async Task StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        if (_process is null || _stdin is null)
            return;

        try
        {
            await _stdin.WriteAsync('q').ConfigureAwait(false);
            await _stdin.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // process may already be exiting
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await _process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        DisposeProcess();
    }

    public Task CaptureSnapshotAsync(CameraConnectionOptions options, string outputFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputFilePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-y -rtsp_transport tcp -i \"{options.RtspUrl}\" -vframes 1 -q:v 1 \"{outputFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start ffmpeg.");
        return process.WaitForExitAsync(cancellationToken);
    }

    public void Dispose() => DisposeProcess();

    private void DisposeProcess()
    {
        try { _stdin?.Dispose(); } catch { /* ignore */ }
        try { _process?.Dispose(); } catch { /* ignore */ }
        _stdin = null;
        _process = null;
    }
}
