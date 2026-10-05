namespace StudioManager.Core.Camera;

/// <summary>
/// Connection settings for an IP CCTV camera (from HCP ONVIF/RTSP usage).
/// </summary>
public sealed class CameraConnectionOptions
{
    public string Host { get; set; } = "196.254.67.11";
    public int RtspPort { get; set; } = 554;
    public int OnvifPort { get; set; } = 8899;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = string.Empty;
    public string RtspPath { get; set; } = "h265";
    public string ProfileToken { get; set; } = "PROFILE_1";
    public string PresetProfileToken { get; set; } = "000";
    public string VideoFolder { get; set; } = string.Empty;
    public string SnapshotFolder { get; set; } = string.Empty;

    public string RtspUrl =>
        string.IsNullOrEmpty(Password)
            ? $"rtsp://{Username}:@{Host}:{RtspPort}/{RtspPath}"
            : $"rtsp://{Username}:{Password}@{Host}:{RtspPort}/{RtspPath}";

    public string OnvifPtzServiceUrl => $"http://{Host}:{OnvifPort}/onvif/ptz_service";
}
