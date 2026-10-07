using System.IO;
using System.Windows;
using StudioManager.App.Services;
using StudioManager.Core.Configuration;
using TextBox = System.Windows.Controls.TextBox;

namespace StudioManager.App.Windows;

public partial class SaveLocationsDialog : Window
{
    private readonly StudioOptions _options;
    private readonly UiSettingsStore _settings;

    public SaveLocationsDialog(StudioOptions options, UiSettingsStore settings)
    {
        _options = options;
        _settings = settings;
        InitializeComponent();

        VideoFolderBox.Text = _options.Camera.VideoFolder;
        SnapshotFolderBox.Text = _options.Camera.SnapshotFolder;
        AudioFolderBox.Text = _options.Audio.SavePath;
        MediaFoldersBox.Text = string.Join(Environment.NewLine, _options.MediaFolders);
    }

    private void BrowseVideo_Click(object sender, RoutedEventArgs e) => BrowseInto(VideoFolderBox);

    private void BrowseSnapshot_Click(object sender, RoutedEventArgs e) => BrowseInto(SnapshotFolderBox);

    private void BrowseAudio_Click(object sender, RoutedEventArgs e) => BrowseInto(AudioFolderBox);

    private static void BrowseInto(TextBox target)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = DirectoryOrEmpty(target.Text),
            ShowNewFolderButton = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            target.Text = dialog.SelectedPath;
    }

    private static string DirectoryOrEmpty(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return path;
        }
        catch
        {
            // ignore
        }

        return string.Empty;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var video = VideoFolderBox.Text.Trim();
        var snapshot = SnapshotFolderBox.Text.Trim();
        var audio = AudioFolderBox.Text.Trim();
        var mediaFolders = MediaFoldersBox.Text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _options.Camera.VideoFolder = video;
        _options.Camera.SnapshotFolder = snapshot;
        _options.Audio.SavePath = audio;
        _options.MediaFolders = [.. mediaFolders];

        _settings.SavePaths(video, snapshot, audio, mediaFolders);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
