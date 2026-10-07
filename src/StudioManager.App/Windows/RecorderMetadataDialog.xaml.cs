using System.Windows;
using System.Windows.Controls;
using StudioManager.App.Services;
using StudioManager.Core.Audio;
using StudioManager.Core.Services;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace StudioManager.App.Windows;

public partial class RecorderMetadataDialog : Window
{
    private readonly AudioCatalogStore _catalogStore;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly List<string> _sources;
    private bool _suppressFileNameUpdate;

    public int SourceIndex { get; private set; }
    public string SourceName { get; private set; } = "";
    public string Album { get; private set; } = "";
    public string Genre { get; private set; } = "";
    public string Singer { get; private set; } = "";
    public string FileName { get; private set; } = "";

    public RecorderMetadataDialog(
        AudioCatalogStore catalogStore,
        IReadOnlyList<string> sources,
        int sourceIndex,
        string album,
        string genre,
        string singer,
        string fileName,
        bool lockSource)
    {
        _catalogStore = catalogStore;
        _sources = sources.ToList();

        InitializeComponent();

        SourceComboBox.ItemsSource = _sources;
        if (_sources.Count > 0)
            SourceComboBox.SelectedIndex = Math.Clamp(sourceIndex, 0, _sources.Count - 1);
        SourceComboBox.IsEnabled = !lockSource;

        AlbumComboBox.ItemsSource = _catalogStore.Catalog.Albums;
        GenreComboBox.ItemsSource = _catalogStore.Catalog.Genres;
        SingerComboBox.ItemsSource = _catalogStore.Catalog.Singers;

        AlbumComboBox.Text = album;
        GenreComboBox.Text = genre;
        SingerComboBox.Text = singer;

        _suppressFileNameUpdate = true;
        FileNameTextBox.Text = string.IsNullOrWhiteSpace(fileName)
            ? BuildDefaultFileName(singer)
            : fileName;
        _suppressFileNameUpdate = false;

        SingerComboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(SingerComboBox_TextChanged));
    }

    private void SingerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => MaybeUpdateFileName();

    private void SingerComboBox_TextChanged(object sender, TextChangedEventArgs e) => MaybeUpdateFileName();

    private void MaybeUpdateFileName()
    {
        if (_suppressFileNameUpdate) return;
        FileNameTextBox.Text = BuildDefaultFileName(SingerComboBox.Text);
    }

    private string BuildDefaultFileName(string? singer)
    {
        var name = string.IsNullOrWhiteSpace(singer) ? _loc["Unknown_Singer"] : singer.Trim();
        return $"{name}-{PersianDateHelper.GetPersianDate(DateTime.Now)}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        SourceIndex = SourceComboBox.SelectedIndex < 0 ? 0 : SourceComboBox.SelectedIndex;
        SourceName = SourceComboBox.SelectedItem as string ?? "";
        Album = AlbumComboBox.Text?.Trim() ?? "";
        Genre = GenreComboBox.Text?.Trim() ?? "";
        Singer = SingerComboBox.Text?.Trim() ?? "";
        FileName = FileNameTextBox.Text?.Trim() ?? "";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
