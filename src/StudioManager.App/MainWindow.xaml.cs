using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using StudioManager.App.Controls;
using StudioManager.App.Services;
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
    private readonly List<RecorderRowControl> _recorders = [];
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private int _nextRecorderNumber = 1;

    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private PreviewMonitorWindow? _monitor2;
    private PreviewMonitorWindow? _monitor3;
    private bool _isLive;
    private bool _ptzBusy;
    private bool _suppressLanguageChange;

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
        SpeedLabel.Text = SpeedValue.ToString("0.0");

        _suppressLanguageChange = true;
        LanguageCombo.SelectedValue = _loc.Culture.Name;
        _suppressLanguageChange = false;

        StatusText.Text = _loc["StatusReady"];
        RefreshRecorderCount();
        _loc.CultureChanged += (_, _) => OnCultureChanged();

        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVlc);
            PreviewView.MediaPlayer = _mediaPlayer;
        }
        catch (Exception ex)
        {
            StatusText.Text = _loc.Format("PreviewEngineUnavailable", ex.Message);
        }
    }

    private CameraConnectionOptions Camera => _options.Camera;

    private double SpeedValue => Math.Clamp(SpeedSlider.Value / 10.0, 0.1, 1.0);

    private void OnCultureChanged()
    {
        RefreshTransportLabels();
        RefreshRecorderCount();
        foreach (var recorder in _recorders)
            recorder.ApplyLocalization();
    }

    private void RefreshTransportLabels()
    {
        LiveButton.Content = _isLive ? _loc["StopLive"] : _loc["Live"];
        RecordButton.Content = _recording.IsRecording ? _loc["StopRecord"] : _loc["Record"];
    }

    private void RefreshRecorderCount()
    {
        var text = _loc.Format("RecordersCount", _recorders.Count);
        RecorderCountText.Text = text;
        if (RecorderCountInline is not null)
            RecorderCountInline.Text = text;
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageChange || LanguageCombo.SelectedValue is not string culture)
            return;

        _loc.SetCulture(culture);
        _options.Ui.Culture = culture;
        StatusText.Text = _loc["StatusReady"];
        RefreshTransportLabels();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedLabel is null) return;
        SpeedLabel.Text = SpeedValue.ToString("0.0");
    }

    private async void LiveToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_mediaPlayer is null || _libVlc is null)
        {
            MessageBox.Show(_loc["Error_LibVlc"], _loc["Dialog_Preview"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isLive)
        {
            _mediaPlayer.Stop();
            _isLive = false;
            LiveButton.Content = _loc["Live"];
            PreviewView.Visibility = Visibility.Collapsed;
            PreviewPlaceholder.Visibility = Visibility.Visible;
            StatusText.Text = _loc["StatusPreviewStopped"];
            return;
        }

        using var media = new Media(_libVlc, Camera.RtspUrl, FromType.FromLocation);
        media.AddOption(":rtsp-tcp");
        media.AddOption(":network-caching=300");
        PreviewPlaceholder.Visibility = Visibility.Collapsed;
        PreviewView.Visibility = Visibility.Visible;
        _mediaPlayer.Play(media);
        _isLive = true;
        LiveButton.Content = _loc["StopLive"];
        StatusText.Text = _loc.Format("StatusLive", Camera.Host);
        await Task.CompletedTask;
    }

    private async void RecordToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_recording.IsRecording)
            {
                await _recording.StopRecordingAsync();
                RecordButton.Content = _loc["Record"];
                StatusText.Text = _loc["StatusRecordingStopped"];
                return;
            }

            var folder = string.IsNullOrWhiteSpace(Camera.VideoFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Studio")
                : Camera.VideoFolder;
            Directory.CreateDirectory(folder);
            var file = Path.Combine(folder, $"rec_{PersianDateHelper.GetPersianDate(DateTime.Now)}_{DateTime.Now:HH-mm-ss}.mp4");
            await _recording.StartRecordingAsync(Camera, file);
            RecordButton.Content = _loc["StopRecord"];
            StatusText.Text = _loc.Format("StatusRecording", file);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Recording"], MessageBoxButton.OK, MessageBoxImage.Error);
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
            StatusText.Text = _loc.Format("StatusSnapshot", file);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Snapshot"], MessageBoxButton.OK, MessageBoxImage.Error);
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
            StatusText.Text = _loc.Format("StatusPtzError", ex.Message);
            _ptzBusy = false;
        }
    }

    private async void Ptz_Up(object sender, MouseButtonEventArgs e) => await StopPtzAsync();

    private async void PtzStop_Click(object sender, RoutedEventArgs e) => await StopPtzAsync();

    private async Task StopPtzAsync()
    {
        try
        {
            await _ptz.StopAsync(Camera);
        }
        catch (Exception ex)
        {
            StatusText.Text = _loc.Format("StatusPtzStop", ex.Message);
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
            StatusText.Text = _loc.Format("StatusPtzError", ex.Message);
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
            MessageBox.Show(_loc["Error_PresetNameRequired"], _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var token = await _ptz.SetPresetAsync(Camera, name);
            _presets.Add(new PtzPreset { Name = name, Token = token });
            PresetNameBox.Clear();
            StatusText.Text = _loc.Format("StatusPresetSaved", name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void GotoPreset_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PtzPreset preset) return;
        try
        {
            await _ptz.GotoPresetAsync(Camera, preset.Token);
            StatusText.Text = _loc.Format("StatusGotoPreset", preset.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
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
            StatusText.Text = _loc.Format("StatusDeletedPreset", preset.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SpawnRecorder_Click(object sender, RoutedEventArgs e)
    {
        var index = _nextRecorderNumber++;
        var catalog = App.Services.GetRequiredService<AudioCatalogStore>();
        var row = new RecorderRowControl(_options.Audio, catalog, index);
        row.SessionCompleted += Recorder_SessionCompleted;
        _recorders.Add(row);
        RecorderRowsPanel.Children.Add(row);
        RefreshRecorderCount();
        StatusText.Text = _loc.Format("StatusOpenedRecorder", index);
    }

    private void Recorder_SessionCompleted(object? sender, EventArgs e)
    {
        if (sender is not RecorderRowControl row)
            return;

        RemoveRecorder(row);
        StatusText.Text = _loc["StatusRecorderSaved"];
    }

    private void RemoveRecorder(RecorderRowControl row)
    {
        row.SessionCompleted -= Recorder_SessionCompleted;
        row.DisposeSession();
        _recorders.Remove(row);
        RecorderRowsPanel.Children.Remove(row);
        RefreshRecorderCount();
    }

    public void ForceStopAllRecorders()
    {
        foreach (var recorder in _recorders.ToList())
            recorder.ForceStopWithoutShutdown();
    }

    private async void ExportUsb_Click(object sender, RoutedEventArgs e)
    {
        var drive = _export.FindRemovableDrive();
        if (drive is null)
        {
            MessageBox.Show(_loc["Error_NoUsb"], _loc["Dialog_Export"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var day = DateOnly.FromDateTime(DateTime.Today);
            var dest = Path.Combine(drive, PersianDateHelper.GetPersianDate(DateTime.Today));
            var count = await _export.CopyTodaysFilesAsync(_options.MediaFolders, dest, day);
            StatusText.Text = _loc.Format("StatusCopied", count, dest);
            MessageBox.Show(_loc.Format("Info_Copied", count, dest), _loc["Dialog_Export"], MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Export"], MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(_loc.Format("Error_MonitorMissing", monitorIndex + 1), _loc["Dialog_Preview"], MessageBoxButton.OK, MessageBoxImage.Information);
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
        ForceStopAllRecorders();
        foreach (var recorder in _recorders.ToList())
            RemoveRecorder(recorder);

        CloseMonitor(ref _monitor2);
        CloseMonitor(ref _monitor3);

        if (_recording.IsRecording)
            await _recording.StopRecordingAsync();

        _mediaPlayer?.Stop();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
    }
}
