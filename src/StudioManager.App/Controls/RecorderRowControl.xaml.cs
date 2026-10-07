using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Lame;
using NAudio.Wave;
using StudioManager.App.Services;
using StudioManager.App.Windows;
using StudioManager.Core.Audio;
using StudioManager.Core.Services;
using UserControl = System.Windows.Controls.UserControl;
using Brush = System.Windows.Media.Brush;

namespace StudioManager.App.Controls;

public partial class RecorderRowControl : UserControl
{
    private const int EqBarCount = 24;

    private readonly AudioRecorderOptions _options;
    private readonly AudioCatalogStore _catalogStore;
    private readonly DispatcherTimer _recordTimer;
    private readonly int _instanceNumber;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly Brush _dotIdle;
    private readonly Brush _dotLive;
    private readonly Brush[] _eqBrushes;
    private readonly Border[] _eqBars = new Border[EqBarCount];
    private readonly List<string> _sources = [];

    private WaveInEvent? _waveIn;
    private LameMP3FileWriter? _mp3Writer;
    private string? _outputFilePath;
    private bool _isRecording;
    private bool _forceStopWithoutShutdown;
    private bool _dialogOpen;
    private int _sourceIndex;
    private string _sourceName = "";
    private string _album = "";
    private string _genre = "";
    private string _singer = "";
    private string _fileName = "";
    private TimeSpan _elapsed;

    public event EventHandler? SessionCompleted;

    public int InstanceNumber => _instanceNumber;

    public RecorderRowControl(AudioRecorderOptions options, AudioCatalogStore catalogStore, int instanceNumber)
    {
        _options = options;
        _catalogStore = catalogStore;
        _instanceNumber = instanceNumber;

        InitializeComponent();

        _dotIdle = (Brush)FindResource("ShellMuted");
        _dotLive = (Brush)FindResource("ShellDanger");
        _eqBrushes =
        [
            (Brush)FindResource("ShellSuccess"),
            (Brush)FindResource("ShellWarn"),
            (Brush)FindResource("ShellDanger")
        ];

        BuildEqualizer();
        Directory.CreateDirectory(_options.SavePath);
        LoadAudioSources();
        _fileName = BuildDefaultFileName(_singer);
        RefreshRowLabels();
        ApplyLocalization();

        _recordTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _recordTimer.Tick += (_, _) =>
        {
            _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
            TimerText.Text = _elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
        };

        if (_options.AutoStartRecording)
            StartRecording();
    }

    public void ApplyLocalization()
    {
        ToggleRecordButton.ToolTip = _isRecording ? _loc["Recorder_Stop"] : _loc["Recorder_Start"];
        RefreshRowLabels();
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

    private void BuildEqualizer()
    {
        EqGrid.Children.Clear();
        for (var i = 0; i < EqBarCount; i++)
        {
            var brush = i < EqBarCount * 0.45 ? _eqBrushes[0]
                : i < EqBarCount * 0.75 ? _eqBrushes[1]
                : _eqBrushes[2];

            var bar = new Border
            {
                Background = brush,
                CornerRadius = new CornerRadius(0.5),
                Margin = new Thickness(0.5, 0, 0.5, 0),
                Height = 3,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.22
            };
            _eqBars[i] = bar;
            EqGrid.Children.Add(bar);
        }
    }

    private void LoadAudioSources()
    {
        _sources.Clear();
        for (var i = 0; i < WaveIn.DeviceCount; i++)
            _sources.Add(WaveIn.GetCapabilities(i).ProductName);

        if (_sources.Count == 0)
        {
            MessageBox.Show(_loc["Recorder_NoDevices"], _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
            IsEnabled = false;
            return;
        }

        _sourceIndex = Math.Min(_instanceNumber - 1, _sources.Count - 1);
        _sourceName = _sources[_sourceIndex];
    }

    private string BuildDefaultFileName(string? singer)
    {
        var name = string.IsNullOrWhiteSpace(singer) ? _loc["Unknown_Singer"] : singer.Trim();
        return $"{name}-{PersianDateHelper.GetPersianDate(DateTime.Now)}";
    }

    private void RefreshRowLabels()
    {
        TitleText.Text = string.IsNullOrWhiteSpace(_fileName)
            ? _loc.Format("Recorder_Title", _instanceNumber)
            : _fileName;

        TipSource.Text = string.IsNullOrWhiteSpace(_sourceName) ? "—" : _sourceName;
        TipSinger.Text = string.IsNullOrWhiteSpace(_singer) ? "—" : _singer;
        TipAlbum.Text = string.IsNullOrWhiteSpace(_album) ? "—" : _album;
        TipGenre.Text = string.IsNullOrWhiteSpace(_genre) ? "—" : _genre;
        TipFile.Text = string.IsNullOrWhiteSpace(_fileName) ? "—" : _fileName;
    }

    private void SetRecordingUi(bool isRecording)
    {
        _isRecording = isRecording;
        ToggleRecordButton.Style = (Style)FindResource(isRecording ? "IconStopButton" : "IconRecordButton");
        ToggleRecordButton.ToolTip = isRecording ? _loc["Recorder_Stop"] : _loc["Recorder_Start"];
        ToggleRecordIcon.Source = new System.Windows.Media.Imaging.BitmapImage(
            new Uri(isRecording
                ? "pack://application:,,,/Assets/Images/stop.png"
                : "pack://application:,,,/Assets/Images/start.png"));
        RecDot.Fill = isRecording ? _dotLive : _dotIdle;
    }

    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (_dialogOpen) return;
        if (e.OriginalSource is DependencyObject source && IsDescendantOf(source, ToggleRecordButton))
            return;

        OpenMetadataDialog();
    }

    private static bool IsDescendantOf(DependencyObject? child, DependencyObject ancestor)
    {
        while (child is not null)
        {
            if (ReferenceEquals(child, ancestor))
                return true;
            child = VisualTreeHelper.GetParent(child);
        }

        return false;
    }

    private void OpenMetadataDialog()
    {
        var owner = Window.GetWindow(this);
        var dialog = new RecorderMetadataDialog(
            _catalogStore,
            _sources,
            _sourceIndex,
            _album,
            _genre,
            _singer,
            _fileName,
            lockSource: _isRecording)
        {
            Owner = owner
        };

        _dialogOpen = true;
        try
        {
            if (dialog.ShowDialog() != true)
                return;

            var sourceChanged = dialog.SourceIndex != _sourceIndex;
            _sourceIndex = dialog.SourceIndex;
            _sourceName = dialog.SourceName;
            _album = dialog.Album;
            _genre = dialog.Genre;
            _singer = dialog.Singer;
            _fileName = dialog.FileName;
            RefreshRowLabels();

            if (sourceChanged && _isRecording)
            {
                CleanupRecording(deleteFile: true);
                StartRecording();
            }
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void ToggleRecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording)
            StopRecording();
        else
            StartRecording();
    }

    private void StopRecording()
    {
        try
        {
            if (_waveIn is not null && _isRecording)
                _waveIn.StopRecording();
        }
        catch (Exception ex)
        {
            MessageBox.Show(_loc.Format("Recorder_StopFailed", ex.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartRecording()
    {
        CleanupRecording(deleteFile: true);

        if (_sources.Count == 0) return;

        var fallback = _loc["Unknown_Singer"];
        var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(_fileName) ? fallback : _fileName.Trim());
        _outputFilePath = GetUniquePath(Path.Combine(_options.SavePath, $"{baseName}.mp3"));

        try
        {
            _waveIn = new WaveInEvent
            {
                DeviceNumber = Math.Clamp(_sourceIndex, 0, _sources.Count - 1),
                WaveFormat = new WaveFormat(_options.SampleRateHz, 16, 2)
            };
            _mp3Writer = new LameMP3FileWriter(_outputFilePath, _waveIn.WaveFormat, _options.Mp3BitrateKbps);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            SetRecordingUi(true);

            _elapsed = TimeSpan.Zero;
            TimerText.Text = "00:00:00";
            _recordTimer.Stop();
            _recordTimer.Start();
        }
        catch (Exception ex)
        {
            CleanupRecording(deleteFile: true);
            SetRecordingUi(false);
            MessageBox.Show(_loc.Format("Recorder_StartFailed", ex.Message), _loc["Recorder_ErrorTitle"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        try
        {
            _mp3Writer?.Write(args.Buffer, 0, args.BytesRecorded);

            float peak = 0f;
            var channels = _waveIn?.WaveFormat.Channels ?? 1;
            var bytesPerSample = _waveIn?.WaveFormat.BitsPerSample == 32 ? 4 : 2;
            var blockAlign = bytesPerSample * channels;

            for (var i = 0; i <= args.BytesRecorded - blockAlign; i += blockAlign)
            {
                for (var c = 0; c < channels; c++)
                {
                    var amp = ReadAmp(args.Buffer, i + c * bytesPerSample, bytesPerSample);
                    if (amp > peak) peak = amp;
                }
            }

            var level = Math.Clamp(peak * 100.0, 0, 100);
            Dispatcher.BeginInvoke(() => UpdateEqualizer(level));
        }
        catch
        {
            // ignore races during shutdown
        }
    }

    private void UpdateEqualizer(double level)
    {
        var lit = (int)Math.Round(EqBarCount * (level / 100.0));
        for (var i = 0; i < EqBarCount; i++)
            _eqBars[i].Opacity = i < lit ? 1.0 : 0.22;
    }

    private void ResetEqualizer()
    {
        foreach (var bar in _eqBars)
            bar.Opacity = 0.22;
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

        Dispatcher.BeginInvoke(() =>
        {
            _recordTimer.Stop();
            ResetEqualizer();
            SetRecordingUi(false);
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
        var uiFileName = _fileName;
        var uiGenre = _genre;
        var uiSinger = _singer;
        var uiAlbum = _album;

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
            {
                Dispatcher.BeginInvoke(() => SessionCompleted?.Invoke(this, EventArgs.Empty));
                return;
            }

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

            Dispatcher.BeginInvoke(() => SessionCompleted?.Invoke(this, EventArgs.Empty));
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

    public void DisposeSession()
    {
        if (_isRecording && _waveIn is not null)
        {
            _forceStopWithoutShutdown = true;
            try { _waveIn.StopRecording(); }
            catch { CleanupRecording(deleteFile: false); }
        }
        else
        {
            CleanupRecording(deleteFile: false);
        }

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
