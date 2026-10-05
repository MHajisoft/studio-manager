namespace StudioManager.Core.Camera;

/// <summary>
/// PTZ velocity in ONVIF ContinuousMove range [-1, 1].
/// </summary>
public readonly record struct PtzVelocity(double Pan, double Tilt, double Zoom)
{
    public static PtzVelocity Stopped => new(0, 0, 0);

    public PtzVelocity Clamp() => new(
        Math.Clamp(Pan, -1.0, 1.0),
        Math.Clamp(Tilt, -1.0, 1.0),
        Math.Clamp(Zoom, -1.0, 1.0));
}
