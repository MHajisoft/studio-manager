using System.Windows;
using System.Windows.Input;
using StudioManager.App.Services;
using StudioManager.Core.Camera;
using StudioManager.Core.Services;

namespace StudioManager.App.Windows;

public partial class PresetsDialog : Window
{
    private readonly IPtzService _ptz;
    private readonly CameraConnectionOptions _camera;
    private readonly PresetStore _presets;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    public PresetsDialog(IPtzService ptz, CameraConnectionOptions camera, PresetStore presets)
    {
        _ptz = ptz;
        _camera = camera;
        _presets = presets;
        InitializeComponent();
        PresetList.ItemsSource = _presets.Presets;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = PresetNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(_loc["Error_PresetNameRequired"], _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var token = await _ptz.SetPresetAsync(_camera, name);
            _presets.Add(new PtzPreset { Name = name, Token = token });
            PresetNameBox.Clear();
            PresetList.SelectedItem = _presets.Presets.FirstOrDefault(p => p.Name == name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Go_Click(object sender, RoutedEventArgs e) => await GotoSelectedAsync();

    private async void PresetList_DoubleClick(object sender, MouseButtonEventArgs e) => await GotoSelectedAsync();

    private async Task GotoSelectedAsync()
    {
        if (PresetList.SelectedItem is not PtzPreset preset)
            return;

        try
        {
            await _ptz.GotoPresetAsync(_camera, preset.Token);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (PresetList.SelectedItem is not PtzPreset preset)
            return;

        try
        {
            await _ptz.RemovePresetAsync(_camera, preset.Token);
            _presets.Remove(preset);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, _loc["Dialog_Preset"], MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
