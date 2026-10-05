using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NAudio.Lame;
using NAudio.Wave;
using StudioManager.App.Services;
using StudioManager.Core.Audio;
using StudioManager.Core.Services;

namespace StudioManager.App.Windows;

public partial class SoundRecorderWindow : Window
{
    private readonly AudioRecorderOptions _options;
    private readonly AudioCatalogStore _catalogStore;
    private readonly DispatcherTimer _recordTimer;
    private readonly int _instanceNumber;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    private WaveInEvent? _waveIn;
    private LameMP3FileWriter? _mp3Writer;
    private string? _outputFilePath;
    private bool _isRecording;
    private bool _isSourceChanging;
    private bool _closeAfterSave;
    private bool _forceStopWithoutShutdown;
    private TimeSpan _elapsed;

    public SoundRecorderWindow(AudioRecorderOptions options, AudioCatalogStore catalogStore, int instanceNumber)
    {
        _options = options;
        _catalogStore = catalogStore;
        _instanceNumber = instanceNumber;

        InitializeComponent();
        ApplyLocalization();

        Directory.CreateDirectory(_options.SavePath);
        LoadAudioSources();
        BindCatalog();
        UpdateFileName();

        _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordTimer.Tick += (_, _) =>
        {
            _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
            RecordingTimeText.Text = _elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        };

        if (_options.AutoStartRecording)
            StartRecording();
    }

    public void ApplyLocalization()
    {
        Title = _isRecording
            ? _loc.Format("Recorder_TitleRecording", _instanceNumber, SourceComboBox.Text)
            : _loc.Format("Recorder_Title", _instanceNumber);
        StartButton.ToolTip = _loc["Recorder_Start"];
        StopButton.ToolTip = _loc["Recorder_Stop"];
    }

    public void ForceStopWithoutShutdown()
    {
        _forceStopWithoutShutdown = true;
        if (_isRecording && _waveIn is not null)
        {
            try { _waveIn.StopRecording(); }
            catch { /* ignore */ }
        }
        else
        {
            CleanupRecording(deleteFile: false);
        }
    }

    private void LoadAudioSources()
    {
        SourceComboBox.Items.Clear();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
            SourceComboBox.Items.Add(WaveIn.GetCapabilities(i).ProductName);

        if (SourceComboBox.Items.Count == 0)
        {
            MessageBox.Show(_loc["Recorder_NoDevices"], _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
            IsEnabled = false;
            return;
        }

        SourceComboBox.SelectedIndex = Math.Min(_instanceNumber - 1, SourceComboBox.Items.Count - 1);
    }

    private void BindCatalog()
    {
        GenreComboBox.ItemsSource = _catalogStore.Catalog.Genres;
        SingerComboBox.ItemsSource = _catalogStore.Catalog.Singers;
        AlbumComboBox.ItemsSource = _catalogStore.Catalog.Albums;
    }

    private void UpdateFileName()
    {
        var singer = string.IsNullOrWhiteSpace(SingerComboBox.Text) ? _loc["Unknown_Singer"] : SingerComboBox.Text.Trim();
        FileNameTextBox.Text = $"{singer}-{PersianDateHelper.GetPersianDate(DateTime.Now)}";
    }

    private void SetButtonStates(bool isRecording)
    {
        StartButton.Visibility = isRecording ? Visibility.Collapsed : Visibility.Visible;
        StopButton.Visibility = isRecording ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SingerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFileName();

    private void SingerComboBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateFileName();

    private void StartButton_Click(object sender, RoutedEventArgs e) => StartRecording();

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_waveIn is not null && _isRecording)
                _waveIn.StopRecording();
            SetButtonStates(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(_loc.Format("Recorder_StopFailed", ex.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartRecording()
    {
        CleanupRecording(deleteFile: true);

        var fallback = _loc["Unknown_Singer"];
        var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(FileNameTextBox.Text) ? fallback : FileNameTextBox.Text.Trim());
        _outputFilePath = GetUniquePath(Path.Combine(_options.SavePath, $"{baseName}.mp3"));

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = SourceComboBox.SelectedIndex,
                WaveFormat = new WaveFormat(_options.SampleRateHz, 16, 2)
            };
            _mp3Writer = new LameMP3FileWriter(_outputFilePath, _waveIn.WaveFormat, _options.Mp3BitrateKbps);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            _isRecording = true;
            SetButtonStates(true);

            _elapsed = TimeSpan.Zero;
            RecordingTimeText.Text = "00:00:00";
            _recordTimer.Stop();
            _recordTimer.Start();
            Title = _loc.Format("Recorder_TitleRecording", _instanceNumber, SourceComboBox.Text);
        }
        catch (Exception ex)
        {
            CleanupRecording(deleteFile: true);
            SetButtonStates(false);
            MessageBox.Show(_loc.Format("Recorder_StartFailed", ex.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        try
        {
            _mp3Writer?.Write(args.Buffer, 0, args.BytesRecorded);

            float leftMax = 0f, rightMax = 0f;
            var channels = _waveIn?.WaveFormat.Channels ?? 1;
            var bytesPerSample = _waveIn?.WaveFormat.BitsPerSample == 32 ? 4 : 2;
            var blockAlign = bytesPerSample * channels;

            for (var i = 0; i <= args.BytesRecorded - blockAlign; i += blockAlign)
            {
                var amp0 = ReadAmp(args.Buffer, i, bytesPerSample);
                if (amp0 > leftMax) leftMax = amp0;

                if (channels > 1)
                {
                    var amp1 = ReadAmp(args.Buffer, i + bytesPerSample, bytesPerSample);
                    if (amp1 > rightMax) rightMax = amp1;
                }
            }

            if (channels == 1)
                rightMax = leftMax;

            Dispatcher.BeginInvoke(() =>
            {
                LeftLevelMeter.Value = Math.Clamp(leftMax * 100.0, 0, 100);
                RightLevelMeter.Value = Math.Clamp(rightMax * 100.0, 0, 100);
            });
        }
        catch
        {
            // ignore races during shutdown
        }
    }

    private static float ReadAmp(byte[] buffer, int offset, int bytesPerSample) =>
        bytesPerSample == 4
            ? Math.Abs(BitConverter.ToInt32(buffer, offset) / 2147483648f)
            : Math.Abs(BitConverter.ToInt16(buffer, offset) / 32768f);

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
        }

        try { _waveIn?.Dispose(); } catch { /* ignore */ }
        _waveIn = null;

        try { _mp3Writer?.Flush(); } catch { /* ignore */ }
        try { _mp3Writer?.Dispose(); } catch { /* ignore */ }
        _mp3Writer = null;
        _isRecording = false;

        Dispatcher.BeginInvoke(() =>
        {
            _recordTimer.Stop();
            LeftLevelMeter.Value = 0;
            RightLevelMeter.Value = 0;
            Title = _loc.Format("Recorder_Title", _instanceNumber);
            SetButtonStates(false);
        });

        if (_forceStopWithoutShutdown)
            return;

        if (e.Exception is not null)
        {
            Dispatcher.BeginInvoke(() =>
                MessageBox.Show(_loc.Format("Recorder_Error", e.Exception.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error));
            return;
        }

        FinalizeRecordingFile();
    }

    private void FinalizeRecordingFile()
    {
        string uiFileName = "", uiGenre = "", uiSinger = "", uiAlbum = "";
        Dispatcher.Invoke(() =>
        {
            uiFileName = FileNameTextBox.Text;
            uiGenre = GenreComboBox.Text;
            uiSinger = SingerComboBox.Text;
            uiAlbum = AlbumComboBox.Text;
        });

        try
        {
            var desiredBase = SanitizeFileName(string.IsNullOrWhiteSpace(uiFileName) ? _loc["Unknown_File"] : uiFileName.Trim());
            var albumFolder = SanitizePathSegment(string.IsNullOrWhiteSpace(uiAlbum) ? _loc["Unknown_Album"] : uiAlbum.Trim());
            var albumDir = Path.Combine(_options.SavePath, albumFolder);
            Directory.CreateDirectory(albumDir);
            var desiredPath = GetUniquePath(Path.Combine(albumDir, $"{desiredBase}.mp3"));

            if (!string.IsNullOrEmpty(_outputFilePath)
                && !string.Equals(_outputFilePath, desiredPath, StringComparison.OrdinalIgnoreCase)
                && File.Exists(_outputFilePath))
            {
                File.Move(_outputFilePath, desiredPath);
                _outputFilePath = desiredPath;
            }
        }
        catch
        {
            // keep original path
        }

        try
        {
            if (string.IsNullOrEmpty(_outputFilePath) || !File.Exists(_outputFilePath))
                return;

            using (var file = TagLib.File.Create(_outputFilePath))
            {
                file.Tag.Title = $"{uiGenre}-{PersianDateHelper.GetPersianDate(DateTime.Now)}";
                file.Tag.Genres = [string.IsNullOrWhiteSpace(uiGenre) ? _loc["Default_Genre"] : uiGenre];
                file.Tag.Performers = [string.IsNullOrWhiteSpace(uiSinger) ? _loc["Unknown_Singer"] : uiSinger];
                file.Tag.Album = string.IsNullOrWhiteSpace(uiAlbum) ? _loc["Unknown_Album"] : uiAlbum;
                file.Tag.Year = PersianDateHelper.GetPersianYear(DateTime.Now);
                file.Save();
            }

            var catalog = _catalogStore.Catalog;
            if (!string.IsNullOrWhiteSpace(uiGenre) && !catalog.Genres.Contains(uiGenre))
                catalog.Genres.Add(uiGenre);
            if (!string.IsNullOrWhiteSpace(uiSinger) && !catalog.Singers.Contains(uiSinger))
                catalog.Singers.Add(uiSinger);
            if (!string.IsNullOrWhiteSpace(uiAlbum) && !catalog.Albums.Contains(uiAlbum))
                catalog.Albums.Add(uiAlbum);
            _catalogStore.Save();

            Dispatcher.BeginInvoke(() =>
            {
                BindCatalog();
                if (_closeAfterSave || _options.CloseAfterSave)
                    Close();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(() =>
                MessageBox.Show(_loc.Format("Recorder_TagFailed", ex.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error));
        }
    }

    private void CleanupRecording(bool deleteFile)
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
        }

        if (_waveIn is not null && _isRecording)
        {
            try { _waveIn.StopRecording(); } catch { /* ignore */ }
        }

        try { _mp3Writer?.Flush(); _mp3Writer?.Dispose(); } catch { /* ignore */ }
        _mp3Writer = null;

        try { _waveIn?.Dispose(); } catch { /* ignore */ }
        _waveIn = null;
        _isRecording = false;

        if (deleteFile && !string.IsNullOrEmpty(_outputFilePath) && File.Exists(_outputFilePath))
        {
            try { File.Delete(_outputFilePath); } catch { /* ignore */ }
        }
    }

    private void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSourceChanging || !_isRecording) return;
        _isSourceChanging = true;
        try
        {
            CleanupRecording(deleteFile: true);
            StartRecording();
        }
        finally
        {
            _isSourceChanging = false;
        }
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_isRecording && _waveIn is not null)
        {
            _closeAfterSave = true;
            try { _waveIn.StopRecording(); } catch { CleanupRecording(deleteFile: false); }
            return;
        }

        CleanupRecording(deleteFile: false);
        _recordTimer.Stop();
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(invalid.Contains(ch) ? '-' : ch);
        var cleaned = Regex.Replace(sb.ToString().Trim().Trim('.'), @"\s+", " ");
        return string.IsNullOrWhiteSpace(cleaned) ? "Unknown" : cleaned;
    }

    private static string SanitizePathSegment(string name) => SanitizeFileName(name);

    private static string GetUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var fileName = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{fileName} ({counter}){ext}");
            counter++;
        } while (File.Exists(candidate));
        return candidate;
    }
}
