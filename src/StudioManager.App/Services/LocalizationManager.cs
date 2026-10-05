using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using StudioManager.App.Localization;

namespace StudioManager.App.Services;

/// <summary>
/// Runtime culture + RTL/LTR manager. Bind UI strings via indexer; bind FlowDirection on roots.
/// </summary>
public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private CultureInfo _culture = CultureInfo.GetCultureInfo("fa-IR");

    private LocalizationManager()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CultureChanged;

    public CultureInfo Culture => _culture;

    public bool IsRtl => _culture.TextInfo.IsRightToLeft;

    public System.Windows.FlowDirection FlowDirection =>
        IsRtl ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

    public string this[string key] =>
        Strings.ResourceManager.GetString(key, _culture) ?? key;

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("fa-IR", "فارسی"),
        new("en-US", "English")
    ];

    public void Initialize(string cultureName)
    {
        SetCulture(string.IsNullOrWhiteSpace(cultureName) ? "fa-IR" : cultureName, raise: false);
    }

    public void SetCulture(string cultureName, bool raise = true)
    {
        try
        {
            _culture = CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            _culture = CultureInfo.GetCultureInfo("fa-IR");
        }

        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;

        if (!raise)
            return;

        OnPropertyChanged(string.Empty);
        OnPropertyChanged("Item[]");
        OnPropertyChanged(nameof(FlowDirection));
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(Culture));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Format(string key, params object[] args)
    {
        var format = this[key];
        try
        {
            return string.Format(_culture, format, args);
        }
        catch (FormatException)
        {
            return format;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record LanguageOption(string Culture, string DisplayName);
