using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using StudioManager.App.Windows;
using StudioManager.Core.Audio;
using StudioManager.Core.Camera;
using StudioManager.Core.Configuration;
using StudioManager.Core.Media;
using StudioManager.Core.Services;

namespace StudioManager.App;

public partial class MainWindow : Window
{
    private readonly StudioOptions _options;
    private readonly IPtzService _ptz;
    private readonly ICameraRecordingService _recording;
    private readonly IMediaExportService _export;
    private readonly PresetStore _presets;
    private readonly ObservableCollection<string> _openRecorders = [];
    private readonly List<SoundRecorderWindow> _recorderWindows = [];

    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private PreviewMonitorWindow? _monitor2;
    private PreviewMonitorWindow? _monitor3;
    private bool _isLive;
    private bool _ptzBusy;

    public MainWindow(
        StudioOptions options,
        IPtzService ptz,
        ICameraRecordingService recording,
        IMediaExportService export,
        PresetStore presets)
    {
        _options = options;
        _ptz = ptz;
        _recording = recording;
        _export = export;
        _presets = presets;

        InitializeComponent();
        PresetList.ItemsSource = _presets.Presets;
        OpenRecordersList.ItemsSource = _openRecorders;
        SpeedLabel.Text = SpeedValue.ToString("0.0");

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
            PreviewView.MediaPlayer = _mediaPlayer;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Preview engine unavailable: {ex.Message}";
        }
    }

    private CameraConnectionOptions Camera => _options.Camera;

    private double SpeedValue => Math.Clamp(SpeedSlider.Value / 10.0, 0.1, 1.0);

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedLabel is null) return;
        SpeedLabel.Text = SpeedValue.ToString("0.0");
    }

    private async void LiveToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is null || _libVlc is null)
        {
            MessageBox.Show("LibVLC failed to initialize. Install/repair VideoLAN.LibVLC.Windows package output.", "Preview", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isLive)
        {
            _mediaPlayer.Stop();
            _isLive = false;
            LiveButton.Content = "Live";
            PreviewPlaceholder.Visibility = Visibility.Visible;
            StatusText.Text = "Preview stopped";
            return;
        }

        using var media = new Media(_libVlc, Camera.RtspUrl, FromType.FromLocation);
        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=300");
        _mediaPlayer.Play(media);
        _isLive = true;
        LiveButton.Content = "Stop live";
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        StatusText.Text = $"Live: {Camera.Host}";
        await Task.CompletedTask;
    }

    private async void RecordToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_recording.IsRecording)
            {
                await _recording.StopRecordingAsync();
                RecordButton.Content = "Record";
                StatusText.Text = "Video recording stopped";
                return;
            }

            var folder = string.IsNullOrWhiteSpace(Camera.VideoFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Studio")
                : Camera.VideoFolder;
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"rec_{PersianDateHelper.GetPersianDate(DateTime.Now)}_{DateTime.Now:HH-mm-ss}.mp4");
            await _recording.StartRecordingAsync(Camera, file);
            RecordButton.Content = "Stop rec";
            StatusText.Text = $"Recording → {file}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Recording", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = string.IsNullOrWhiteSpace(Camera.SnapshotFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Studio")
                : Camera.SnapshotFolder;
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"{PersianDateHelper.GetPersianDate(DateTime.Now)}_{DateTime.Now:HH-mm-ss}.jpg");
            await _recording.CaptureSnapshotAsync(Camera, file);
            StatusText.Text = $"Snapshot → {file}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Snapshot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Ptz_Down(object sender, MouseButtonEventArgs e)
    {
        if (_ptzBusy || sender is not FrameworkElement { Tag: string tag })
            return;

        var parts = tag.Split(',');
        if (parts.Length != 3) return;

        var pan = double.Parse(parts[0]) * SpeedValue;
        var tilt = double.Parse(parts[1]) * SpeedValue;
        var zoom = double.Parse(parts[2]) * SpeedValue;

        _ptzBusy = true;
        try
        {
            await _ptz.ContinuousMoveAsync(Camera, new PtzVelocity(pan, tilt, zoom));
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PTZ error: {ex.Message}";
            _ptzBusy = false;
        }
    }

    private async void Ptz_Up(object sender, MouseButtonEventArgs e)
    {
        await StopPtzAsync();
    }

    private async void PtzStop_Click(object sender, RoutedEventArgs e) => await StopPtzAsync();

    private async Task StopPtzAsync()
    {
        try
        {
            await _ptz.StopAsync(Camera);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PTZ stop: {ex.Message}";
        }
        finally
        {
            _ptzBusy = false;
        }
    }

    private async void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_ptzBusy) return;

        PtzVelocity? velocity = e.Key switch
        {
            Key.Left => new PtzVelocity(-SpeedValue, 0, 0),
            Key.Right => new PtzVelocity(SpeedValue, 0, 0),
            Key.Up => new PtzVelocity(0, SpeedValue, 0),
            Key.Down => new PtzVelocity(0, -SpeedValue, 0),
            Key.Add or Key.OemPlus => new PtzVelocity(0, 0, SpeedValue),
            Key.Subtract or Key.OemMinus => new PtzVelocity(0, 0, -SpeedValue),
            Key.Escape => PtzVelocity.Stopped,
            _ => null
        };

        if (velocity is null) return;
        e.Handled = true;

        if (e.Key == Key.Escape)
        {
            await StopPtzAsync();
            return;
        }

        _ptzBusy = true;
        try
        {
            await _ptz.ContinuousMoveAsync(Camera, velocity.Value);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"PTZ error: {ex.Message}";
            _ptzBusy = false;
        }
    }

    private async void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Add or Key.OemPlus or Key.Subtract or Key.OemMinus)
        {
            e.Handled = true;
            await StopPtzAsync();
        }
    }

    private async void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Enter a preset name.", "Preset", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var token = await _ptz.SetPresetAsync(Camera, name);
            _presets.Add(new PtzPreset { Name = name, Token = token });
            PresetNameBox.Clear();
            StatusText.Text = $"Preset saved: {name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Preset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void GotoPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PtzPreset preset) return;
        try
        {
            await _ptz.GotoPresetAsync(Camera, preset.Token);
            StatusText.Text = $"Goto preset: {preset.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Preset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void PresetList_DoubleClick(object sender, MouseButtonEventArgs e) => GotoPreset_Click(sender, e);

    private async void DeletePreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PtzPreset preset) return;
        try
        {
            await _ptz.RemovePresetAsync(Camera, preset.Token);
            _presets.Remove(preset);
            StatusText.Text = $"Deleted preset: {preset.Name}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Preset", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SpawnRecorder_Click(object sender, RoutedEventArgs e)
    {
        var index = _recorderWindows.Count + 1;
        var catalog = App.Services.GetRequiredService<AudioCatalogStore>();
        var window = new SoundRecorderWindow(_options.Audio, catalog, index)
        {
            Owner = this
        };
        window.Closed += (_, _) =>
        {
            _recorderWindows.Remove(window);
            RefreshRecorderList();
        };
        _recorderWindows.Add(window);
        window.Show();
        RefreshRecorderList();
        StatusText.Text = $"Opened sound recorder #{index}";
    }

    private void RefreshRecorderList()
    {
        _openRecorders.Clear();
        foreach (var w in _recorderWindows)
            _openRecorders.Add(w.Title);
        RecorderCountText.Text = $"Recorders: {_recorderWindows.Count}";
    }

    private async void ExportUsb_Click(object sender, RoutedEventArgs e)
    {
        var drive = _export.FindRemovableDrive();
        if (drive is null)
        {
            MessageBox.Show("No removable USB drive found.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var day = DateOnly.FromDateTime(DateTime.Today);
            var dest = Path.Combine(drive, PersianDateHelper.GetPersianDate(DateTime.Today).Replace('-', '-'));
            var count = await _export.CopyTodaysFilesAsync(_options.MediaFolders, dest, day);
            StatusText.Text = $"Copied {count} file(s) to {dest}";
            MessageBox.Show($"Copied {count} file(s) to {dest}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Monitor2_Changed(object sender, RoutedEventArgs e)
    {
        if (Monitor2Check.IsChecked == true)
            ShowMonitor(ref _monitor2, 1);
        else
            CloseMonitor(ref _monitor2);
    }

    private void Monitor3_Changed(object sender, RoutedEventArgs e)
    {
        if (Monitor3Check.IsChecked == true)
            ShowMonitor(ref _monitor3, 2);
        else
            CloseMonitor(ref _monitor3);
    }

    private void ShowMonitor(ref PreviewMonitorWindow? window, int monitorIndex)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        if (monitorIndex >= screens.Length)
        {
            MessageBox.Show($"Monitor {monitorIndex + 1} is not available.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            if (monitorIndex == 1) Monitor2Check.IsChecked = false;
            if (monitorIndex == 2) Monitor3Check.IsChecked = false;
            return;
        }

        window ??= new PreviewMonitorWindow(_libVlc, Camera.RtspUrl);
        var bounds = screens[monitorIndex].Bounds;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        window.Show();
        window.Start();
    }

    private static void CloseMonitor(ref PreviewMonitorWindow? window)
    {
        if (window is null) return;
        window.Stop();
        window.Close();
        window = null;
    }

    private async void Window_Closed(object? sender, EventArgs e)
    {
        CloseMonitor(ref _monitor2);
        CloseMonitor(ref _monitor3);

        if (_recording.IsRecording)
            await _recording.StopRecordingAsync();

        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}
