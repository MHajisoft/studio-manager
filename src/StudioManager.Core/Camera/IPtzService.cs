namespace StudioManager.Core.Camera;

/// <summary>
/// ONVIF PTZ control (HCP: ContinuousMove / Stop / SetPreset / GotoPreset / RemovePreset).
/// </summary>
public interface IPtzService
{
    Task ContinuousMoveAsync(CameraConnectionOptions options, PtzVelocity velocity, CancellationToken cancellationToken = default);
    Task StopAsync(CameraConnectionOptions options, CancellationToken cancellationToken = default);
    Task<string> SetPresetAsync(CameraConnectionOptions options, string? name = null, CancellationToken cancellationToken = default);
    Task GotoPresetAsync(CameraConnectionOptions options, string presetToken, CancellationToken cancellationToken = default);
    Task RemovePresetAsync(CameraConnectionOptions options, string presetToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PtzPreset>> GetPresetsAsync(CameraConnectionOptions options, CancellationToken cancellationToken = default);
}
