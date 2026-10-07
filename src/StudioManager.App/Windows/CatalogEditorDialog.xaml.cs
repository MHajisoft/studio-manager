using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using StudioManager.App.Services;
using StudioManager.Core.Services;
using TextBox = System.Windows.Controls.TextBox;
using ListBox = System.Windows.Controls.ListBox;

namespace StudioManager.App.Windows;

public partial class CatalogEditorDialog : Window
{
    private readonly AudioCatalogStore _store;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private readonly ObservableCollection<string> _singers;
    private readonly ObservableCollection<string> _albums;
    private readonly ObservableCollection<string> _genres;

    public CatalogEditorDialog(AudioCatalogStore store)
    {
        _store = store;
        InitializeComponent();

        _singers = new ObservableCollection<string>(_store.Catalog.Singers.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase));
        _albums = new ObservableCollection<string>(_store.Catalog.Albums.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase));
        _genres = new ObservableCollection<string>(_store.Catalog.Genres.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase));

        SingerList.ItemsSource = _singers;
        AlbumList.ItemsSource = _albums;
        GenreList.ItemsSource = _genres;
    }

    private void SingerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SingerList.SelectedItem is string value)
            SingerInput.Text = value;
    }

    private void AlbumList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AlbumList.SelectedItem is string value)
            AlbumInput.Text = value;
    }

    private void GenreList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GenreList.SelectedItem is string value)
            GenreInput.Text = value;
    }

    private void AddSinger_Click(object sender, RoutedEventArgs e) => AddItem(_singers, SingerInput);
    private void AddAlbum_Click(object sender, RoutedEventArgs e) => AddItem(_albums, AlbumInput);
    private void AddGenre_Click(object sender, RoutedEventArgs e) => AddItem(_genres, GenreInput);

    private void RenameSinger_Click(object sender, RoutedEventArgs e) => RenameItem(_singers, SingerList, SingerInput);
    private void RenameAlbum_Click(object sender, RoutedEventArgs e) => RenameItem(_albums, AlbumList, AlbumInput);
    private void RenameGenre_Click(object sender, RoutedEventArgs e) => RenameItem(_genres, GenreList, GenreInput);

    private void DeleteSinger_Click(object sender, RoutedEventArgs e) => DeleteItem(_singers, SingerList, SingerInput);
    private void DeleteAlbum_Click(object sender, RoutedEventArgs e) => DeleteItem(_albums, AlbumList, AlbumInput);
    private void DeleteGenre_Click(object sender, RoutedEventArgs e) => DeleteItem(_genres, GenreList, GenreInput);

    private void AddItem(ObservableCollection<string> items, TextBox input)
    {
        var value = input.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(_loc["Catalog_EmptyItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (items.Any(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(_loc["Catalog_DuplicateItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        items.Add(value);
        input.Clear();
    }

    private void RenameItem(ObservableCollection<string> items, ListBox list, TextBox input)
    {
        if (list.SelectedItem is not string selected)
        {
            MessageBox.Show(_loc["Catalog_SelectItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var value = input.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(value))
        {
            MessageBox.Show(_loc["Catalog_EmptyItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.Equals(selected, value, StringComparison.CurrentCultureIgnoreCase)
            && items.Any(x => string.Equals(x, value, StringComparison.CurrentCultureIgnoreCase)))
        {
            MessageBox.Show(_loc["Catalog_DuplicateItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var index = items.IndexOf(selected);
        items[index] = value;
        list.SelectedItem = value;
    }

    private void DeleteItem(ObservableCollection<string> items, ListBox list, TextBox input)
    {
        if (list.SelectedItem is not string selected)
        {
            MessageBox.Show(_loc["Catalog_SelectItem"], _loc["Catalog_Title"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        items.Remove(selected);
        input.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _store.Catalog.Singers = _singers.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        _store.Catalog.Albums = _albums.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        _store.Catalog.Genres = _genres.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase).ToList();
        _store.Save();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
