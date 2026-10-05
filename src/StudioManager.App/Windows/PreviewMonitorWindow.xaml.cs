using System.Windows;
using LibVLCSharp.Shared;

namespace StudioManager.App.Windows;

public partial class PreviewMonitorWindow : Window
{
    private readonly LibVLC? _libVlc;
    private readonly string _url;
    private MediaPlayer? _player;
    private readonly bool _ownsLibVlc;

    public PreviewMonitorWindow(LibVLC? sharedLibVlc, string rtspUrl)
    {
        InitializeComponent();
        _url = rtspUrl;

        if (sharedLibVlc is null)
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC();
            _ownsLibVlc = true;
        }
        else
        {
            _libVlc = sharedLibVlc;
            _ownsLibVlc = false;
        }
    }

    public void Start()
    {
        if (_libVlc is null) return;
        _player ??= new MediaPlayer(_libVlc);
        Video.MediaPlayer = _player;
        using var media = new Media(_libVlc, _url, FromType.FromLocation);
        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=300");
        _player.Play(media);
    }

    public void Stop()
    {
        _player?.Stop();
    }

    protected override void OnClosed(EventArgs e)
    {
        _player?.Stop();
        _player?.Dispose();
        if (_ownsLibVlc)
            _libVlc?.Dispose();
        base.OnClosed(e);
    }
}
